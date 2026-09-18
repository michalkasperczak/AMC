//! Wiązanie natywnych `play_request_id` Librespota z `playId` nadawanym przez AMC
//! oraz składanie zdarzeń `state` / `ended` / `trackError`.
//!
//! Moduł jest CZYSTY (bez typów Librespota), żeby dał się testować bez konta,
//! bez dźwięku i bez sieci. `main.rs` tylko tłumaczy `PlayerEvent` na wywołania.
//!
//! Powody istnienia:
//! * `Player::load()` nic nie zwraca, a `PlayerEvent::PlayRequestIdChanged`
//!   przynosi natywny `u64` dopiero asynchronicznie - trzeba go dowiązać do
//!   `playId`, który AMC nadał wcześniej;
//! * zdarzenia poprzedniego odtwarzania mogą dotrzeć PO wydaniu nowej komendy
//!   `play` i muszą zostać odrzucone, także gdy dwa razy pod rząd leci to samo URI;
//! * pauza i stop NIE mogą wygenerować `ended`.

use serde_json::{json, Value};

use crate::protocol::SESSION_ID;

/// Zdarzenia wejściowe (znormalizowane odpowiedniki `librespot` `PlayerEvent`).
#[derive(Debug, Clone, PartialEq)]
pub enum Incoming {
    PlayRequestIdChanged { native_id: u64 },
    Loading { native_id: u64, position_ms: u32 },
    TrackChanged { uri: String, duration_ms: u32 },
    Playing { native_id: u64, position_ms: u32 },
    Paused { native_id: u64, position_ms: u32 },
    PositionChanged { native_id: u64, position_ms: u32 },
    Seeked { native_id: u64, position_ms: u32 },
    /// Zatrzymanie na życzenie (komenda `stop`). NIE jest końcem utworu.
    Stopped { native_id: u64 },
    EndOfTrack { native_id: u64 },
    Unavailable { native_id: u64 },
}

/// Stan jednego zlecenia odtwarzania z punktu widzenia AMC.
#[derive(Debug, Clone)]
struct Slot {
    play_id: i64,
    uri: String,
    native_id: Option<u64>,
    position_ms: u32,
    duration_ms: Option<u32>,
    is_playing: bool,
    is_paused: bool,
    /// Ustawiane gdy AMC poprosiło o stop - blokuje `ended` z resztek zdarzeń.
    stop_requested: bool,
    /// Zapamiętane, żeby nie wysłać `ended` dwa razy dla tego samego playId.
    ended_emitted: bool,
}

impl Slot {
    fn state_event(&self) -> Value {
        json!({
            "type": "state",
            "sessionId": SESSION_ID,
            "playId": self.play_id,
            "uri": self.uri,
            "positionMs": self.position_ms,
            "durationMs": self.duration_ms,
            "isPlaying": self.is_playing,
            "isPaused": self.is_paused
        })
    }

    fn ended_event(&self) -> Value {
        json!({
            "type": "ended",
            "sessionId": SESSION_ID,
            "playId": self.play_id,
            "uri": self.uri
        })
    }

    fn track_error_event(&self, code: &str) -> Value {
        json!({
            "type": "trackError",
            "sessionId": SESSION_ID,
            "playId": self.play_id,
            "uri": self.uri,
            "code": code
        })
    }
}

/// Mapuje natywne identyfikatory Librespota na `playId` AMC.
#[derive(Debug, Default)]
pub struct PlayTracker {
    /// Ostatnie zlecenie AMC, do którego jeszcze nie dowiązano natywnego id.
    pending: Option<Slot>,
    /// Zlecenie aktualnie dowiązane do natywnego `play_request_id`.
    current: Option<Slot>,
}

impl PlayTracker {
    pub fn new() -> Self {
        Self::default()
    }

    /// AMC wydało `play`. Poprzednie zlecenie przestaje być bieżące od razu,
    /// żeby jego spóźnione zdarzenia nie udawały nowego odtwarzania.
    pub fn begin_play(&mut self, play_id: i64, uri: &str, position_ms: u32) {
        self.pending = Some(Slot {
            play_id,
            uri: uri.to_string(),
            native_id: None,
            position_ms,
            duration_ms: None,
            is_playing: false,
            is_paused: false,
            stop_requested: false,
            ended_emitted: false,
        });
        // Bieżące zlecenie zostaje do rozpoznania starych zdarzeń, ale traci prawo
        // do emitowania czegokolwiek - patrz `is_current`.
        if let Some(cur) = self.current.as_mut() {
            cur.stop_requested = true;
        }
    }

    /// AMC poprosiło o `stop`: blokujemy `ended` dla bieżącego zlecenia.
    pub fn request_stop(&mut self) {
        if let Some(cur) = self.current.as_mut() {
            cur.stop_requested = true;
            cur.is_playing = false;
            cur.is_paused = false;
        }
        self.pending = None;
    }

    /// Czy `native_id` należy do zlecenia, o którym AMC ma jeszcze słuchać.
    fn is_current(&self, native_id: u64) -> bool {
        matches!(self.current.as_ref(), Some(c) if c.native_id == Some(native_id))
    }

    pub fn current_play_id(&self) -> Option<i64> {
        self.current.as_ref().map(|c| c.play_id)
    }

    /// Przetwarza jedno zdarzenie i zwraca zdarzenia do wypisania na stdout.
    /// Zdarzenia nienależące do bieżącego zlecenia są milcząco odrzucane.
    pub fn handle(&mut self, event: Incoming) -> Vec<Value> {
        match event {
            Incoming::PlayRequestIdChanged { native_id } => {
                // Dowiązanie: pierwszy taki komunikat po naszym `play` należy do niego.
                if let Some(mut slot) = self.pending.take() {
                    slot.native_id = Some(native_id);
                    self.current = Some(slot);
                }
                // Bez oczekującego zlecenia to nie nasza sprawa (np. gapless upstreamu).
                vec![]
            }
            Incoming::TrackChanged { uri, duration_ms } => {
                // Długość bierzemy z AudioItem; przypisujemy tylko gdy URI się zgadza.
                match self.current.as_mut() {
                    Some(cur) if cur.uri == uri => {
                        cur.duration_ms = Some(duration_ms);
                        vec![cur.state_event()]
                    }
                    _ => vec![],
                }
            }
            Incoming::Loading {
                native_id,
                position_ms,
            } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                if cur.stop_requested {
                    return vec![];
                }
                cur.position_ms = position_ms;
                cur.is_playing = false;
                cur.is_paused = false;
                vec![cur.state_event()]
            }
            Incoming::Playing {
                native_id,
                position_ms,
            }
            | Incoming::Seeked {
                native_id,
                position_ms,
            }
            | Incoming::PositionChanged {
                native_id,
                position_ms,
            } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                if cur.stop_requested {
                    return vec![];
                }
                cur.position_ms = position_ms;
                cur.is_playing = true;
                cur.is_paused = false;
                vec![cur.state_event()]
            }
            Incoming::Paused {
                native_id,
                position_ms,
            } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                if cur.stop_requested {
                    return vec![];
                }
                cur.position_ms = position_ms;
                cur.is_playing = false;
                cur.is_paused = true;
                // Pauza nigdy nie generuje `ended`.
                vec![cur.state_event()]
            }
            Incoming::Stopped { native_id } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                cur.is_playing = false;
                cur.is_paused = false;
                // Stop nigdy nie generuje `ended`.
                vec![cur.state_event()]
            }
            Incoming::EndOfTrack { native_id } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                if cur.stop_requested || cur.ended_emitted {
                    return vec![];
                }
                cur.ended_emitted = true;
                cur.is_playing = false;
                cur.is_paused = false;
                vec![cur.ended_event()]
            }
            Incoming::Unavailable { native_id } => {
                if !self.is_current(native_id) {
                    return vec![];
                }
                let cur = self.current.as_mut().expect("is_current");
                cur.is_playing = false;
                cur.is_paused = false;
                vec![cur.track_error_event(crate::protocol::code::TRACK_UNAVAILABLE)]
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const URI: &str = "spotify:track:4cOdK2wGLETKBW3PvgPWqT";

    fn types(events: &[Value]) -> Vec<String> {
        events
            .iter()
            .map(|e| e["type"].as_str().unwrap_or("?").to_string())
            .collect()
    }

    #[test]
    fn native_id_is_bound_to_amc_play_id() {
        let mut t = PlayTracker::new();
        t.begin_play(101, URI, 0);
        assert!(t.handle(Incoming::PlayRequestIdChanged { native_id: 7 }).is_empty());
        let out = t.handle(Incoming::Playing {
            native_id: 7,
            position_ms: 250,
        });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["playId"], json!(101));
        assert_eq!(out[0]["sessionId"], json!(SESSION_ID));
        assert_eq!(out[0]["positionMs"], json!(250));
        assert_eq!(out[0]["isPlaying"], json!(true));
    }

    #[test]
    fn duration_comes_from_track_changed() {
        let mut t = PlayTracker::new();
        t.begin_play(1, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 1 });
        let out = t.handle(Incoming::TrackChanged {
            uri: URI.to_string(),
            duration_ms: 214_000,
        });
        assert_eq!(out[0]["durationMs"], json!(214_000));
    }

    #[test]
    fn stale_events_from_previous_play_are_dropped_same_uri_twice() {
        // Kluczowy przypadek: to samo URI odtwarzane dwa razy pod rząd.
        let mut t = PlayTracker::new();
        t.begin_play(1, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 10 });
        assert_eq!(
            types(&t.handle(Incoming::Playing {
                native_id: 10,
                position_ms: 1000
            })),
            ["state"]
        );

        // Drugie odtworzenie TEGO SAMEGO URI z nowym playId i nowym natywnym id.
        t.begin_play(2, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 11 });

        // Spóźnione zdarzenia starego zlecenia: żadnego wyjścia.
        assert!(t
            .handle(Incoming::Playing {
                native_id: 10,
                position_ms: 5000
            })
            .is_empty());
        assert!(t.handle(Incoming::EndOfTrack { native_id: 10 }).is_empty());

        // Nowe zlecenie działa i raportuje swój playId.
        let out = t.handle(Incoming::Playing {
            native_id: 11,
            position_ms: 10,
        });
        assert_eq!(out[0]["playId"], json!(2));
        let ended = t.handle(Incoming::EndOfTrack { native_id: 11 });
        assert_eq!(types(&ended), ["ended"]);
        assert_eq!(ended[0]["playId"], json!(2));
        assert_eq!(ended[0]["uri"], json!(URI));
        assert_eq!(ended[0]["sessionId"], json!(SESSION_ID));
    }

    #[test]
    fn pause_does_not_end_track() {
        let mut t = PlayTracker::new();
        t.begin_play(5, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 3 });
        t.handle(Incoming::Playing {
            native_id: 3,
            position_ms: 100,
        });
        let out = t.handle(Incoming::Paused {
            native_id: 3,
            position_ms: 100,
        });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["isPaused"], json!(true));
        assert_eq!(out[0]["isPlaying"], json!(false));
    }

    #[test]
    fn stop_does_not_end_track_and_suppresses_later_end_of_track() {
        let mut t = PlayTracker::new();
        t.begin_play(6, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 4 });
        t.handle(Incoming::Playing {
            native_id: 4,
            position_ms: 100,
        });
        t.request_stop();
        let out = t.handle(Incoming::Stopped { native_id: 4 });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["isPlaying"], json!(false));
        // Nawet jeśli silnik dorzuci EndOfTrack, AMC nie zobaczy `ended`.
        assert!(t.handle(Incoming::EndOfTrack { native_id: 4 }).is_empty());
    }

    #[test]
    fn ended_is_emitted_once() {
        let mut t = PlayTracker::new();
        t.begin_play(8, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 9 });
        assert_eq!(types(&t.handle(Incoming::EndOfTrack { native_id: 9 })), ["ended"]);
        assert!(t.handle(Incoming::EndOfTrack { native_id: 9 }).is_empty());
    }

    #[test]
    fn unavailable_maps_to_track_error() {
        let mut t = PlayTracker::new();
        t.begin_play(12, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 2 });
        let out = t.handle(Incoming::Unavailable { native_id: 2 });
        assert_eq!(types(&out), ["trackError"]);
        assert_eq!(out[0]["playId"], json!(12));
        assert_eq!(out[0]["sessionId"], json!(SESSION_ID));
        assert_eq!(out[0]["code"], json!("trackUnavailable"));
    }

    // --- Wyścig 1: dwa `play` przed pierwszym PlayRequestIdChanged ---
    //
    // Upstream (playback/src/player.rs, handle_command_load) wysyła
    // PlayRequestIdChanged RAZ na każdy `load`, w kolejności komend. Gdy AMC wyda
    // dwa `play` pod rząd, pierwsze PlayRequestIdChanged NALEŻY do pierwszego
    // zlecenia i nie wolno go dowiązać do drugiego.

    const URI_B: &str = "spotify:track:1301WleyT98MSxVHPZCA6M";

    #[test]
    fn first_native_id_is_not_stolen_by_the_second_play() {
        let mut t = PlayTracker::new();
        t.begin_play(1, URI, 0);
        t.begin_play(2, URI_B, 0);

        // To id należy do zlecenia 1, które AMC już porzuciło - nic nie emitujemy.
        t.handle(Incoming::PlayRequestIdChanged { native_id: 10 });
        assert!(
            t.handle(Incoming::Playing {
                native_id: 10,
                position_ms: 1000
            })
            .is_empty(),
            "zdarzenia porzuconego zlecenia 1 nie mogą udawać zlecenia 2"
        );
        assert!(t.handle(Incoming::EndOfTrack { native_id: 10 }).is_empty());

        // Dopiero drugie id należy do zlecenia 2.
        t.handle(Incoming::PlayRequestIdChanged { native_id: 11 });
        let out = t.handle(Incoming::Playing {
            native_id: 11,
            position_ms: 20,
        });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["playId"], json!(2));
        assert_eq!(out[0]["uri"], json!(URI_B));
        assert_eq!(t.current_play_id(), Some(2));
    }

    #[test]
    fn stop_between_two_loads_keeps_binding_order() {
        let mut t = PlayTracker::new();
        t.begin_play(1, URI, 0);
        t.request_stop();
        t.begin_play(2, URI_B, 0);

        // Spóźnione PlayRequestIdChanged zatrzymanego zlecenia 1.
        t.handle(Incoming::PlayRequestIdChanged { native_id: 10 });
        assert!(t
            .handle(Incoming::Playing {
                native_id: 10,
                position_ms: 500
            })
            .is_empty());

        // Zlecenie 2 musi dostać swoje id i normalnie raportować.
        t.handle(Incoming::PlayRequestIdChanged { native_id: 11 });
        let out = t.handle(Incoming::Playing {
            native_id: 11,
            position_ms: 30,
        });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["playId"], json!(2));
        let ended = t.handle(Incoming::EndOfTrack { native_id: 11 });
        assert_eq!(types(&ended), ["ended"]);
        assert_eq!(ended[0]["playId"], json!(2));
    }

    #[test]
    fn two_queued_loads_of_the_same_uri_bind_in_order() {
        // To samo URI dwa razy: po URI nie da się ich rozróżnić, tylko po kolejności.
        let mut t = PlayTracker::new();
        t.begin_play(1, URI, 0);
        t.begin_play(2, URI, 5_000);

        t.handle(Incoming::PlayRequestIdChanged { native_id: 10 });
        assert!(t
            .handle(Incoming::TrackChanged {
                uri: URI.to_string(),
                duration_ms: 214_000
            })
            .is_empty(),
            "TrackChanged porzuconego zlecenia nie może opisywać nowego playId"
        );
        t.handle(Incoming::PlayRequestIdChanged { native_id: 11 });

        let out = t.handle(Incoming::TrackChanged {
            uri: URI.to_string(),
            duration_ms: 214_000,
        });
        assert_eq!(out[0]["playId"], json!(2));
        assert_eq!(out[0]["durationMs"], json!(214_000));
        assert_eq!(out[0]["positionMs"], json!(5_000));
    }

    // --- Wyścig 2: Seeked/PositionChanged nie zmieniają pauzy w granie ---
    //
    // Upstream wysyła Seeked zarówno ze stanu Playing, JAK I Paused
    // (player.rs, handle_command_seek: `PlayerState::Playing { .. } | PlayerState::Paused { .. }`),
    // więc seek na pauzie nie oznacza wznowienia odtwarzania.

    #[test]
    fn seek_while_paused_does_not_resume_playback() {
        let mut t = PlayTracker::new();
        t.begin_play(5, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 3 });
        t.handle(Incoming::Playing {
            native_id: 3,
            position_ms: 100,
        });
        t.handle(Incoming::Paused {
            native_id: 3,
            position_ms: 100,
        });

        let out = t.handle(Incoming::Seeked {
            native_id: 3,
            position_ms: 42_000,
        });
        assert_eq!(types(&out), ["state"]);
        assert_eq!(out[0]["positionMs"], json!(42_000));
        assert_eq!(out[0]["isPaused"], json!(true), "seek na pauzie nie wznawia");
        assert_eq!(out[0]["isPlaying"], json!(false), "seek na pauzie nie wznawia");
    }

    #[test]
    fn position_changed_while_paused_does_not_resume_playback() {
        let mut t = PlayTracker::new();
        t.begin_play(6, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 4 });
        t.handle(Incoming::Paused {
            native_id: 4,
            position_ms: 7_000,
        });
        let out = t.handle(Incoming::PositionChanged {
            native_id: 4,
            position_ms: 7_100,
        });
        assert_eq!(out[0]["positionMs"], json!(7_100));
        assert_eq!(out[0]["isPlaying"], json!(false));
        assert_eq!(out[0]["isPaused"], json!(true));
    }

    #[test]
    fn seek_while_playing_stays_playing() {
        let mut t = PlayTracker::new();
        t.begin_play(7, URI, 0);
        t.handle(Incoming::PlayRequestIdChanged { native_id: 5 });
        t.handle(Incoming::Playing {
            native_id: 5,
            position_ms: 100,
        });
        let out = t.handle(Incoming::Seeked {
            native_id: 5,
            position_ms: 90_000,
        });
        assert_eq!(out[0]["positionMs"], json!(90_000));
        assert_eq!(out[0]["isPlaying"], json!(true));
        assert_eq!(out[0]["isPaused"], json!(false));
    }

    #[test]
    fn events_without_binding_are_ignored() {
        let mut t = PlayTracker::new();
        assert!(t
            .handle(Incoming::Playing {
                native_id: 99,
                position_ms: 1
            })
            .is_empty());
        assert!(t.handle(Incoming::EndOfTrack { native_id: 99 }).is_empty());
        assert_eq!(t.current_play_id(), None);
    }
}
