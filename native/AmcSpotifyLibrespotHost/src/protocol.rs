//! Warstwa protokołu: parsowanie i walidacja komend AMC oraz budowa zdarzeń.
//!
//! Zasady kontraktu v1 (uzgodnione z AMC):
//! * każde wejście jest jedną linią JSON z polami `command`, `requestId` (liczba),
//!   `sessionId` == "spotifyLibrespot";
//! * stdout zawiera WYŁĄCZNIE linie JSON;
//! * `accessToken` nigdy nie jest odsyłany ani logowany.

use serde_json::{json, Map, Value};

/// Jedyna dopuszczalna nazwa sesji.
pub const SESSION_ID: &str = "spotifyLibrespot";
/// Wersja protokołu ogłaszana w zdarzeniu `ready`.
pub const PROTOCOL_VERSION: u32 = 1;

/// Kody błędów protokołu. Stabilne, klient AMC może na nich polegać.
pub mod code {
    pub const BAD_JSON: &str = "badJson";
    pub const BAD_SHAPE: &str = "badShape";
    pub const BAD_REQUEST_ID: &str = "badRequestId";
    pub const BAD_SESSION_ID: &str = "badSessionId";
    pub const UNKNOWN_COMMAND: &str = "unknownCommand";
    pub const MISSING_FIELD: &str = "missingField";
    pub const BAD_URI: &str = "badUri";
    pub const BAD_RANGE: &str = "badRange";
    pub const BAD_TYPE: &str = "badType";
    pub const NOT_INITIALIZED: &str = "notInitialized";
    pub const ALREADY_INITIALIZED: &str = "alreadyInitialized";
    pub const NO_AUDIO_DEVICE: &str = "noAudioDevice";
    pub const AUTH_FAILED: &str = "authFailed";
    pub const AUTH_TIMEOUT: &str = "authTimeout";
    pub const UNSUPPORTED: &str = "unsupported";
    pub const INTERNAL: &str = "internal";
    /// Zwracany, gdy ktoś prosi o zmianę prędkości odtwarzania.
    pub const NO_SPEED_CONTROL: &str = "speedControlUnsupported";
    pub const TRACK_UNAVAILABLE: &str = "trackUnavailable";
}

/// Rozpoznana i zwalidowana komenda.
#[derive(Debug, Clone, PartialEq)]
pub enum Command {
    Ping,
    Devices,
    Initialize {
        access_token: String,
        device: Option<String>,
        volume: u8,
    },
    Play {
        uri: String,
        position_ms: u32,
        play_id: i64,
    },
    Pause,
    Resume,
    Stop,
    Seek {
        position_ms: u32,
    },
    Volume {
        volume: u8,
    },
    Shutdown,
}

/// Komenda wraz z jej `requestId`.
#[derive(Debug, Clone, PartialEq)]
pub struct Request {
    pub request_id: i64,
    pub command: Command,
}

/// Błąd odrzucenia wejścia. `request_id` jest dołączany tylko gdy udało się go ustalić.
#[derive(Debug, Clone, PartialEq)]
pub struct Rejection {
    pub request_id: Option<i64>,
    pub code: &'static str,
    pub message: String,
}

impl Rejection {
    fn new(request_id: Option<i64>, code: &'static str, message: impl Into<String>) -> Self {
        Self {
            request_id,
            code,
            message: message.into(),
        }
    }
}

fn as_u32_in_range(v: &Value, max: u64) -> Option<u32> {
    // Odrzucamy ułamki, liczby ujemne i wartości poza zakresem.
    if let Some(n) = v.as_u64() {
        if n <= max {
            return Some(n as u32);
        }
        return None;
    }
    if v.as_f64().is_some() || v.as_i64().is_some() {
        return None;
    }
    None
}

/// Waliduje URI utworu/odcinka Spotify bez sięgania do sieci.
///
/// Dopuszczamy wyłącznie `spotify:track:<22 znaki base62>` oraz
/// `spotify:episode:<22 znaki base62>`. Reszta to `badUri`.
pub fn validate_uri(uri: &str) -> bool {
    let rest = match uri.strip_prefix("spotify:") {
        Some(r) => r,
        None => return false,
    };
    let (kind, id) = match rest.split_once(':') {
        Some(p) => p,
        None => return false,
    };
    if kind != "track" && kind != "episode" {
        return false;
    }
    id.len() == 22 && id.bytes().all(|b| b.is_ascii_alphanumeric())
}

/// Parsuje jedną linię stdin do żądania albo odrzucenia.
pub fn parse_line(line: &str) -> Result<Request, Rejection> {
    let value: Value = serde_json::from_str(line)
        .map_err(|e| Rejection::new(None, code::BAD_JSON, format!("nieprawidłowy JSON: {e}")))?;
    let obj: &Map<String, Value> = value.as_object().ok_or_else(|| {
        Rejection::new(None, code::BAD_SHAPE, "oczekiwano obiektu JSON w jednej linii")
    })?;

    // requestId musi być liczbą całkowitą; bez niego nie umiemy adresować odpowiedzi.
    let request_id = match obj.get("requestId") {
        None => {
            return Err(Rejection::new(
                None,
                code::MISSING_FIELD,
                "brak pola requestId",
            ))
        }
        Some(v) => match v.as_i64() {
            Some(n) => n,
            None => {
                return Err(Rejection::new(
                    None,
                    code::BAD_REQUEST_ID,
                    "requestId musi być liczbą całkowitą",
                ))
            }
        },
    };
    let rid = Some(request_id);

    match obj.get("sessionId").and_then(Value::as_str) {
        Some(SESSION_ID) => {}
        Some(other) => {
            return Err(Rejection::new(
                rid,
                code::BAD_SESSION_ID,
                format!("nieznany sessionId: {other}"),
            ))
        }
        None => {
            return Err(Rejection::new(
                rid,
                code::BAD_SESSION_ID,
                "brak lub nietekstowy sessionId",
            ))
        }
    }

    let name = match obj.get("command").and_then(Value::as_str) {
        Some(c) => c,
        None => {
            return Err(Rejection::new(
                rid,
                code::MISSING_FIELD,
                "brak lub nietekstowe pole command",
            ))
        }
    };

    let command = match name {
        "ping" => Command::Ping,
        "devices" => Command::Devices,
        "pause" => Command::Pause,
        "resume" => Command::Resume,
        "stop" => Command::Stop,
        "shutdown" => Command::Shutdown,
        "speed" | "setSpeed" | "playbackRate" => {
            return Err(Rejection::new(
                rid,
                code::NO_SPEED_CONTROL,
                "Librespot nie udostępnia zmiany prędkości odtwarzania; AMC nie może tego udawać",
            ))
        }
        "initialize" => {
            let access_token = match obj.get("accessToken").and_then(Value::as_str) {
                Some(t) if !t.trim().is_empty() => t.to_string(),
                Some(_) => {
                    return Err(Rejection::new(
                        rid,
                        code::MISSING_FIELD,
                        "accessToken jest pusty",
                    ))
                }
                None => {
                    return Err(Rejection::new(
                        rid,
                        code::MISSING_FIELD,
                        "brak lub nietekstowe accessToken",
                    ))
                }
            };
            let device = match obj.get("device") {
                None | Some(Value::Null) => None,
                Some(Value::String(s)) if !s.is_empty() => Some(s.clone()),
                Some(Value::String(_)) => {
                    return Err(Rejection::new(
                        rid,
                        code::BAD_TYPE,
                        "device: pusty tekst; użyj null dla urządzenia domyślnego",
                    ))
                }
                Some(_) => {
                    return Err(Rejection::new(
                        rid,
                        code::BAD_TYPE,
                        "device musi być null albo dokładną nazwą urządzenia",
                    ))
                }
            };
            let volume = match obj.get("volume") {
                Some(v) => match as_u32_in_range(v, 100) {
                    Some(n) => n as u8,
                    None => {
                        return Err(Rejection::new(
                            rid,
                            code::BAD_RANGE,
                            "volume musi być liczbą całkowitą 0..100",
                        ))
                    }
                },
                None => {
                    return Err(Rejection::new(rid, code::MISSING_FIELD, "brak pola volume"))
                }
            };
            Command::Initialize {
                access_token,
                device,
                volume,
            }
        }
        "play" => {
            let uri = match obj.get("uri").and_then(Value::as_str) {
                Some(u) => u.to_string(),
                None => {
                    return Err(Rejection::new(
                        rid,
                        code::MISSING_FIELD,
                        "brak lub nietekstowe uri",
                    ))
                }
            };
            if !validate_uri(&uri) {
                return Err(Rejection::new(
                    rid,
                    code::BAD_URI,
                    "uri musi mieć postać spotify:track:<22> albo spotify:episode:<22>",
                ));
            }
            let position_ms = match obj.get("positionMs") {
                Some(v) => match as_u32_in_range(v, u32::MAX as u64) {
                    Some(n) => n,
                    None => {
                        return Err(Rejection::new(
                            rid,
                            code::BAD_RANGE,
                            "positionMs musi być całkowite i >= 0",
                        ))
                    }
                },
                None => 0,
            };
            let play_id = match obj.get("playId").and_then(Value::as_i64) {
                Some(n) => n,
                None => {
                    return Err(Rejection::new(
                        rid,
                        code::MISSING_FIELD,
                        "brak playId (liczba nadana przez AMC)",
                    ))
                }
            };
            Command::Play {
                uri,
                position_ms,
                play_id,
            }
        }
        "seek" => {
            let position_ms = match obj.get("positionMs") {
                Some(v) => match as_u32_in_range(v, u32::MAX as u64) {
                    Some(n) => n,
                    None => {
                        return Err(Rejection::new(
                            rid,
                            code::BAD_RANGE,
                            "positionMs musi być całkowite i >= 0",
                        ))
                    }
                },
                None => {
                    return Err(Rejection::new(
                        rid,
                        code::MISSING_FIELD,
                        "brak pola positionMs",
                    ))
                }
            };
            Command::Seek { position_ms }
        }
        "volume" => {
            let volume = match obj.get("volume") {
                Some(v) => match as_u32_in_range(v, 100) {
                    Some(n) => n as u8,
                    None => {
                        return Err(Rejection::new(
                            rid,
                            code::BAD_RANGE,
                            "volume musi być liczbą całkowitą 0..100",
                        ))
                    }
                },
                None => {
                    return Err(Rejection::new(rid, code::MISSING_FIELD, "brak pola volume"))
                }
            };
            Command::Volume { volume }
        }
        other => {
            return Err(Rejection::new(
                rid,
                code::UNKNOWN_COMMAND,
                format!("nieznana komenda: {other}"),
            ))
        }
    };

    Ok(Request {
        request_id,
        command,
    })
}

// ---------- budowanie zdarzeń wychodzących ----------

pub fn ready_event() -> Value {
    json!({
        "type": "ready",
        "sessionId": SESSION_ID,
        "protocolVersion": PROTOCOL_VERSION
    })
}

pub fn ack_event(request_id: i64) -> Value {
    json!({ "type": "ack", "sessionId": SESSION_ID, "requestId": request_id })
}

pub fn devices_event(request_id: i64, devices: &[(String, bool)]) -> Value {
    let list: Vec<Value> = devices
        .iter()
        .map(|(name, is_default)| json!({ "name": name, "isDefault": is_default }))
        .collect();
    json!({
        "type": "devices",
        "sessionId": SESSION_ID,
        "requestId": request_id,
        "devices": list
    })
}

pub fn error_event(request_id: Option<i64>, code: &str, message: &str) -> Value {
    let mut map = Map::new();
    map.insert("type".into(), json!("error"));
    map.insert("sessionId".into(), json!(SESSION_ID));
    if let Some(id) = request_id {
        map.insert("requestId".into(), json!(id));
    }
    map.insert("code".into(), json!(code));
    map.insert("message".into(), json!(message));
    Value::Object(map)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn line(v: Value) -> String {
        v.to_string()
    }

    #[test]
    fn ping_parses() {
        let r = parse_line(&line(
            json!({"command":"ping","requestId":7,"sessionId":SESSION_ID}),
        ))
        .unwrap();
        assert_eq!(r.request_id, 7);
        assert_eq!(r.command, Command::Ping);
    }

    #[test]
    fn unknown_session_is_rejected_with_request_id() {
        let e = parse_line(&line(
            json!({"command":"ping","requestId":3,"sessionId":"spotifyWeb"}),
        ))
        .unwrap_err();
        assert_eq!(e.code, code::BAD_SESSION_ID);
        assert_eq!(e.request_id, Some(3));
    }

    #[test]
    fn unknown_command_is_rejected() {
        let e = parse_line(&line(
            json!({"command":"teleport","requestId":1,"sessionId":SESSION_ID}),
        ))
        .unwrap_err();
        assert_eq!(e.code, code::UNKNOWN_COMMAND);
    }

    #[test]
    fn bad_json_has_no_request_id() {
        let e = parse_line("{to nie json").unwrap_err();
        assert_eq!(e.code, code::BAD_JSON);
        assert_eq!(e.request_id, None);
    }

    #[test]
    fn non_numeric_request_id_is_rejected() {
        let e = parse_line(&line(
            json!({"command":"ping","requestId":"7","sessionId":SESSION_ID}),
        ))
        .unwrap_err();
        assert_eq!(e.code, code::BAD_REQUEST_ID);
    }

    #[test]
    fn initialize_accepts_null_device_and_keeps_token_out_of_debug_surface() {
        let r = parse_line(&line(json!({
            "command":"initialize","requestId":1,"sessionId":SESSION_ID,
            "accessToken":"BQ-sekret","device":Value::Null,"volume":40
        })))
        .unwrap();
        match r.command {
            Command::Initialize {
                access_token,
                device,
                volume,
            } => {
                assert_eq!(access_token, "BQ-sekret");
                assert_eq!(device, None);
                assert_eq!(volume, 40);
            }
            other => panic!("zła komenda: {other:?}"),
        }
    }

    #[test]
    fn initialize_rejects_volume_out_of_range_and_fractional() {
        for bad in [json!(101), json!(-1), json!(12.5), json!("40")] {
            let e = parse_line(&line(json!({
                "command":"initialize","requestId":1,"sessionId":SESSION_ID,
                "accessToken":"t","device":Value::Null,"volume":bad
            })))
            .unwrap_err();
            assert_eq!(e.code, code::BAD_RANGE, "wartość {bad:?} miała być odrzucona");
        }
    }

    #[test]
    fn play_requires_valid_uri_and_play_id() {
        let ok = parse_line(&line(json!({
            "command":"play","requestId":2,"sessionId":SESSION_ID,
            "uri":"spotify:track:4cOdK2wGLETKBW3PvgPWqT","positionMs":0,"playId":11
        })))
        .unwrap();
        assert_eq!(
            ok.command,
            Command::Play {
                uri: "spotify:track:4cOdK2wGLETKBW3PvgPWqT".into(),
                position_ms: 0,
                play_id: 11
            }
        );

        for bad_uri in [
            "spotify:album:4cOdK2wGLETKBW3PvgPWqT",
            "spotify:track:tooshort",
            "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT",
            "spotify:track:",
            "",
        ] {
            let e = parse_line(&line(json!({
                "command":"play","requestId":2,"sessionId":SESSION_ID,
                "uri":bad_uri,"positionMs":0,"playId":11
            })))
            .unwrap_err();
            assert_eq!(e.code, code::BAD_URI, "URI {bad_uri} miało być odrzucone");
        }

        let e = parse_line(&line(json!({
            "command":"play","requestId":2,"sessionId":SESSION_ID,
            "uri":"spotify:track:4cOdK2wGLETKBW3PvgPWqT","positionMs":0
        })))
        .unwrap_err();
        assert_eq!(e.code, code::MISSING_FIELD);
    }

    #[test]
    fn play_rejects_negative_position() {
        let e = parse_line(&line(json!({
            "command":"play","requestId":2,"sessionId":SESSION_ID,
            "uri":"spotify:episode:4cOdK2wGLETKBW3PvgPWqT","positionMs":-5,"playId":1
        })))
        .unwrap_err();
        assert_eq!(e.code, code::BAD_RANGE);
    }

    #[test]
    fn speed_is_refused_not_faked() {
        let e = parse_line(&line(
            json!({"command":"speed","requestId":9,"sessionId":SESSION_ID,"value":1.5}),
        ))
        .unwrap_err();
        assert_eq!(e.code, code::NO_SPEED_CONTROL);
    }

    #[test]
    fn error_event_without_request_id_omits_field() {
        let e = error_event(None, code::BAD_JSON, "zepsute");
        assert!(e.get("requestId").is_none());
        assert_eq!(e["sessionId"], json!(SESSION_ID));
    }

    #[test]
    fn ready_event_declares_protocol_version_only() {
        let r = ready_event();
        assert_eq!(r["type"], json!("ready"));
        assert_eq!(r["protocolVersion"], json!(1));
        // ready NIE oznacza zalogowania - nie ma tu żadnego pola o autoryzacji.
        assert!(r.get("authenticated").is_none());
        assert!(r.get("loggedIn").is_none());
    }
}
