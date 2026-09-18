//! Proces pomocniczy AMC dla DRUGIEJ sesji odtwarzania: "Spotify - Librespot".
//!
//! Komendy JSON po stdin (jedna linia = jedna komenda), zdarzenia JSON po stdout
//! (jedna linia = jedno zdarzenie). Nic innego na stdout nie trafia - logi idą na stderr.
//!
//! Świadome granice (nie udajemy ich obsługi):
//! * brak sterowania prędkością odtwarzania - komenda `speed` jest odrzucana kodem
//!   `speedControlUnsupported`, bo Librespot 0.8.0 tego nie ma;
//! * brak discovery/Spirc i brak otwierania przeglądarki - wrapper steruje `Player`
//!   bezpośrednio, token dostaje wyłącznie po stdin;
//! * `ready` mówi tylko o wersji protokołu i NIE oznacza zalogowania;
//! * `ack` dla `initialize` wysyłamy dopiero po potwierdzonym `Session::connect`;
//! * `ack` dla `play` znaczy "komenda przyjęta do kolejki", nie "leci dźwięk" -
//!   faktyczny start AMC rozpoznaje po `state` z `isPlaying: true`.

mod protocol;
mod tracker;

use std::sync::Arc;
use std::time::Duration;

use serde_json::Value;
use tokio::io::{AsyncBufReadExt, BufReader};
use tokio::sync::mpsc;

use librespot_core::authentication::Credentials;
use librespot_core::config::SessionConfig;
use librespot_core::session::Session;
use librespot_core::spotify_uri::SpotifyUri;
use librespot_playback::audio_backend;
use librespot_playback::config::{AudioFormat, Bitrate, PlayerConfig, VolumeCtrl};
use librespot_playback::mixer::{softmixer::SoftMixer, Mixer, MixerConfig};
use librespot_playback::player::{Player, PlayerEvent};

use protocol::{code, Command, Rejection, Request};
use tracker::{Incoming, PlayTracker};

/// Ile najdłużej czekamy na `Session::connect`. Po tym czasie jest błąd, nigdy fałszywe ready.
const CONNECT_TIMEOUT: Duration = Duration::from_secs(30);
/// Jak często Librespot ma raportować pozycję odtwarzania.
const POSITION_UPDATE_INTERVAL: Duration = Duration::from_millis(1000);

/// Wypisuje jedno zdarzenie jako linię JSON na stdout i natychmiast opróżnia bufor.
fn emit(value: &Value) {
    use std::io::Write;
    let mut out = std::io::stdout().lock();
    // serde_json nie wstawia znaków nowej linii w środku - linia zostaje jedną linią.
    let _ = writeln!(out, "{value}");
    let _ = out.flush();
}

/// Lista urządzeń wyjściowych: (nazwa, czy domyślne). Nie wymaga logowania.
fn list_output_devices() -> Result<Vec<(String, bool)>, String> {
    use cpal::traits::{DeviceTrait, HostTrait};
    let host = cpal::default_host();
    let default_name = host
        .default_output_device()
        .and_then(|d| d.name().ok());
    let devices = host
        .output_devices()
        .map_err(|e| format!("nie udało się odczytać urządzeń wyjściowych: {e}"))?;
    let mut out = Vec::new();
    for device in devices {
        if let Ok(name) = device.name() {
            let is_default = Some(&name) == default_name.as_ref();
            out.push((name, is_default));
        }
    }
    Ok(out)
}

/// Przelicza 0..100 z AMC na skalę Librespota (0..u16::MAX).
fn volume_pct_to_raw(pct: u8) -> u16 {
    let pct = pct.min(100) as u32;
    ((pct * u16::MAX as u32) / 100) as u16
}

/// Wszystko, co powstaje dopiero po udanym `initialize`.
struct Engine {
    player: Arc<Player>,
    mixer: Arc<dyn Mixer>,
    #[allow(dead_code)]
    session: Session,
}

impl Engine {
    /// Buduje sesję, mikser i Player. Zwraca błąd (kod, komunikat) bez tokenu w treści.
    async fn start(
        access_token: &str,
        device: Option<String>,
        volume_pct: u8,
        event_tx: mpsc::UnboundedSender<PlayerEvent>,
    ) -> Result<Self, (&'static str, String)> {
        // Nazwę urządzenia sprawdzamy PRZED logowaniem - szybciej i bez sieci.
        if let Some(name) = device.as_deref() {
            let devices = list_output_devices().map_err(|e| (code::NO_AUDIO_DEVICE, e))?;
            if !devices.iter().any(|(n, _)| n == name) {
                return Err((
                    code::NO_AUDIO_DEVICE,
                    format!("nie ma urządzenia wyjściowego o nazwie: {name}"),
                ));
            }
        }

        let session = Session::new(SessionConfig::default(), None);
        let credentials = Credentials::with_access_token(access_token);

        // Ograniczone czekanie. Odmowa logowania to komunikat, nie fałszywe ready.
        match tokio::time::timeout(CONNECT_TIMEOUT, session.connect(credentials, false)).await {
            Ok(Ok(())) => {}
            Ok(Err(e)) => {
                // Komunikat Librespota nie zawiera tokenu, ale i tak nie przepuszczamy
                // niczego, co wyglądałoby na sekret - patrz `scrub`.
                return Err((code::AUTH_FAILED, scrub(&e.to_string(), access_token)));
            }
            Err(_) => {
                return Err((
                    code::AUTH_TIMEOUT,
                    format!(
                        "Librespot nie potwierdził logowania w {} s",
                        CONNECT_TIMEOUT.as_secs()
                    ),
                ))
            }
        }

        // Prawdziwe sterowanie głośnością: soft mixer Librespota.
        // NoOpVolume bez innego sterowania byłby ciszą albo stałym poziomem - niedopuszczalne.
        let mixer_config = MixerConfig {
            volume_ctrl: VolumeCtrl::Log(VolumeCtrl::DEFAULT_DB_RANGE),
            ..MixerConfig::default()
        };
        let mixer: Arc<dyn Mixer> = Arc::new(
            SoftMixer::open(mixer_config)
                .map_err(|e| (code::INTERNAL, format!("mikser softvol nie wstał: {e}")))?,
        );
        mixer.set_volume(volume_pct_to_raw(volume_pct));

        let player_config = PlayerConfig {
            // Jawnie najwyższa jakość: bez tego Librespot bierze 160 kbps.
            bitrate: Bitrate::Bitrate320,
            // Jawnie: bez tego nie ma PositionChanged, a AMC potrzebuje pozycji.
            position_update_interval: Some(POSITION_UPDATE_INTERVAL),
            ..PlayerConfig::default()
        };

        let sink_builder = audio_backend::find(Some("rodio".to_string()))
            .ok_or_else(|| (code::INTERNAL, "brak backendu audio rodio w tym buildzie".to_string()))?;
        let format = AudioFormat::default();
        let player = Player::new(
            player_config,
            session.clone(),
            mixer.get_soft_volume(),
            move || sink_builder(device, format),
        );

        // Przepompowanie zdarzeń Librespota do naszej pętli.
        let mut channel = player.get_player_event_channel();
        tokio::spawn(async move {
            while let Some(event) = channel.recv().await {
                if event_tx.send(event).is_err() {
                    break;
                }
            }
        });

        Ok(Self {
            player,
            mixer,
            session,
        })
    }
}

/// Usuwa z komunikatu ewentualny token, gdyby biblioteka go wplotła.
fn scrub(message: &str, token: &str) -> String {
    if token.is_empty() {
        return message.to_string();
    }
    message.replace(token, "[token usunięty]")
}

/// Tłumaczy `PlayerEvent` na nasze znormalizowane `Incoming`.
/// Zdarzenia sesyjne/mieszane (shuffle, repeat itp.) nie dotyczą tej sesji.
fn normalize(event: PlayerEvent) -> Option<Incoming> {
    Some(match event {
        PlayerEvent::PlayRequestIdChanged { play_request_id } => Incoming::PlayRequestIdChanged {
            native_id: play_request_id,
        },
        PlayerEvent::Loading {
            play_request_id,
            position_ms,
            ..
        } => Incoming::Loading {
            native_id: play_request_id,
            position_ms,
        },
        PlayerEvent::TrackChanged { audio_item } => Incoming::TrackChanged {
            uri: audio_item.uri.clone(),
            duration_ms: audio_item.duration_ms,
        },
        PlayerEvent::Playing {
            play_request_id,
            position_ms,
            ..
        } => Incoming::Playing {
            native_id: play_request_id,
            position_ms,
        },
        PlayerEvent::Paused {
            play_request_id,
            position_ms,
            ..
        } => Incoming::Paused {
            native_id: play_request_id,
            position_ms,
        },
        PlayerEvent::PositionChanged {
            play_request_id,
            position_ms,
            ..
        }
        | PlayerEvent::PositionCorrection {
            play_request_id,
            position_ms,
            ..
        } => Incoming::PositionChanged {
            native_id: play_request_id,
            position_ms,
        },
        PlayerEvent::Seeked {
            play_request_id,
            position_ms,
            ..
        } => Incoming::Seeked {
            native_id: play_request_id,
            position_ms,
        },
        PlayerEvent::Stopped {
            play_request_id, ..
        } => Incoming::Stopped {
            native_id: play_request_id,
        },
        PlayerEvent::EndOfTrack {
            play_request_id, ..
        } => Incoming::EndOfTrack {
            native_id: play_request_id,
        },
        PlayerEvent::Unavailable {
            play_request_id, ..
        } => Incoming::Unavailable {
            native_id: play_request_id,
        },
        _ => return None,
    })
}

#[tokio::main(flavor = "multi_thread", worker_threads = 2)]
async fn main() {
    // Logi Librespota tylko na stderr; stdout jest zarezerwowany na protokół.
    env_logger::Builder::from_env(
        env_logger::Env::default().default_filter_or("librespot=warn,warn"),
    )
    .target(env_logger::Target::Stderr)
    .format_timestamp_millis()
    .init();

    // Tryb pomocniczy dla CI i diagnostyki: wypisz urządzenia i wyjdź.
    // Nie wymaga konta ani tokenu.
    if std::env::args().any(|a| a == "--list-devices") {
        match list_output_devices() {
            Ok(devices) => {
                emit(&protocol::devices_event(0, &devices));
                return;
            }
            Err(e) => {
                emit(&protocol::error_event(Some(0), code::NO_AUDIO_DEVICE, &e));
                std::process::exit(2);
            }
        }
    }

    emit(&protocol::ready_event());

    let (event_tx, mut event_rx) = mpsc::unbounded_channel::<PlayerEvent>();
    let mut engine: Option<Engine> = None;
    let mut playtracker = PlayTracker::new();

    let mut lines = BufReader::new(tokio::io::stdin()).lines();

    loop {
        tokio::select! {
            // Zdarzenia silnika.
            maybe_event = event_rx.recv() => {
                match maybe_event {
                    Some(event) => {
                        if let Some(incoming) = normalize(event) {
                            for out in playtracker.handle(incoming) {
                                emit(&out);
                            }
                        }
                    }
                    None => {
                        // Kanał zamknięty - silnik odpadł; dalej obsługujemy tylko stdin.
                    }
                }
            }
            // Komendy AMC.
            line = lines.next_line() => {
                let line = match line {
                    Ok(Some(l)) => l,
                    // Utrata stdin: kończymy i zwalniamy audio.
                    Ok(None) => break,
                    Err(e) => {
                        emit(&protocol::error_event(None, code::INTERNAL, &format!("błąd odczytu stdin: {e}")));
                        break;
                    }
                };
                if line.trim().is_empty() {
                    continue;
                }

                let request = match protocol::parse_line(&line) {
                    Ok(r) => r,
                    Err(Rejection { request_id, code: c, message }) => {
                        emit(&protocol::error_event(request_id, c, &message));
                        continue;
                    }
                };

                let Request { request_id, command } = request;

                match command {
                    Command::Ping => emit(&protocol::ack_event(request_id)),

                    Command::Devices => match list_output_devices() {
                        Ok(devices) => emit(&protocol::devices_event(request_id, &devices)),
                        Err(e) => emit(&protocol::error_event(Some(request_id), code::NO_AUDIO_DEVICE, &e)),
                    },

                    Command::Initialize { access_token, device, volume } => {
                        if engine.is_some() {
                            emit(&protocol::error_event(Some(request_id), code::ALREADY_INITIALIZED,
                                "sesja jest już zainicjowana"));
                            continue;
                        }
                        match Engine::start(&access_token, device, volume, event_tx.clone()).await {
                            // ack dopiero po POTWIERDZONYM connect.
                            Ok(e) => { engine = Some(e); emit(&protocol::ack_event(request_id)); }
                            Err((c, msg)) => emit(&protocol::error_event(Some(request_id), c, &msg)),
                        }
                    }

                    Command::Play { uri, position_ms, play_id } => {
                        let Some(eng) = engine.as_ref() else {
                            emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED,
                                "najpierw initialize"));
                            continue;
                        };
                        // Walidacja składni już przeszła; tu tłumaczymy na typ Librespota.
                        let parsed = match SpotifyUri::from_uri(&uri) {
                            Ok(u) if u.is_playable() => u,
                            Ok(_) => {
                                emit(&protocol::error_event(Some(request_id), code::BAD_URI,
                                    "to nie jest odtwarzalny element (oczekiwano utworu albo odcinka)"));
                                continue;
                            }
                            Err(e) => {
                                emit(&protocol::error_event(Some(request_id), code::BAD_URI,
                                    &format!("Librespot odrzucił uri: {e}")));
                                continue;
                            }
                        };
                        // Najpierw wiążemy playId, POTEM ładujemy - inaczej pierwsze
                        // PlayRequestIdChanged mogłoby przyjść przed dowiązaniem.
                        playtracker.begin_play(play_id, &uri, position_ms);
                        eng.player.load(parsed, true, position_ms);
                        // ack = komenda zakolejkowana, nie "leci dźwięk".
                        emit(&protocol::ack_event(request_id));
                    }

                    Command::Pause => match engine.as_ref() {
                        Some(eng) => { eng.player.pause(); emit(&protocol::ack_event(request_id)); }
                        None => emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED, "najpierw initialize")),
                    },

                    Command::Resume => match engine.as_ref() {
                        Some(eng) => { eng.player.play(); emit(&protocol::ack_event(request_id)); }
                        None => emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED, "najpierw initialize")),
                    },

                    Command::Stop => match engine.as_ref() {
                        Some(eng) => {
                            playtracker.request_stop();
                            eng.player.stop();
                            emit(&protocol::ack_event(request_id));
                        }
                        None => emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED, "najpierw initialize")),
                    },

                    Command::Seek { position_ms } => match engine.as_ref() {
                        Some(eng) => { eng.player.seek(position_ms); emit(&protocol::ack_event(request_id)); }
                        None => emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED, "najpierw initialize")),
                    },

                    Command::Volume { volume } => match engine.as_ref() {
                        Some(eng) => {
                            eng.mixer.set_volume(volume_pct_to_raw(volume));
                            emit(&protocol::ack_event(request_id));
                        }
                        None => emit(&protocol::error_event(Some(request_id), code::NOT_INITIALIZED, "najpierw initialize")),
                    },

                    Command::Shutdown => {
                        emit(&protocol::ack_event(request_id));
                        break;
                    }
                }
            }
        }
    }

    // Zwolnienie audio: zatrzymaj Player, potem porzuć sesję.
    if let Some(eng) = engine.take() {
        eng.player.stop();
        drop(eng);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn volume_mapping_hits_the_ends() {
        assert_eq!(volume_pct_to_raw(0), 0);
        assert_eq!(volume_pct_to_raw(100), u16::MAX);
        let mid = volume_pct_to_raw(50);
        assert!(mid > 32_000 && mid < 33_000, "środek skali: {mid}");
        assert!(volume_pct_to_raw(30) < volume_pct_to_raw(70));
    }

    #[test]
    fn soft_mixer_really_changes_attenuation() {
        // Dowód, że głośność działa przez soft mixer, a nie NoOpVolume
        // (NoOpVolume zawsze zwraca 1.0 i byłby niedopuszczalny).
        let mixer = SoftMixer::open(MixerConfig {
            volume_ctrl: VolumeCtrl::Log(VolumeCtrl::DEFAULT_DB_RANGE),
            ..MixerConfig::default()
        })
        .expect("softmixer");
        let getter = mixer.get_soft_volume();

        mixer.set_volume(volume_pct_to_raw(100));
        let loud = getter.attenuation_factor();
        mixer.set_volume(volume_pct_to_raw(25));
        let quiet = getter.attenuation_factor();
        mixer.set_volume(volume_pct_to_raw(0));
        let muted = getter.attenuation_factor();

        assert!((loud - 1.0).abs() < 1e-9, "100% ma dawać 1.0, było {loud}");
        assert!(quiet < loud, "25% ma być cichsze niż 100%: {quiet} vs {loud}");
        assert_eq!(muted, 0.0, "0% ma być ciszą, było {muted}");
    }

    #[test]
    fn scrub_removes_token_from_messages() {
        let msg = scrub("logowanie odrzucone dla BQ-tajne", "BQ-tajne");
        assert!(!msg.contains("BQ-tajne"));
        assert!(msg.contains("[token usunięty]"));
    }

    #[test]
    fn rodio_backend_is_present_in_this_build() {
        // Pilnuje, że build nie zgubił backendu audio przez złe features.
        assert!(audio_backend::find(Some("rodio".to_string())).is_some());
    }
}
