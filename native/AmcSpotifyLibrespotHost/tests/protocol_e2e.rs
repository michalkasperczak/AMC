//! Testy protokołu end-to-end: uruchamiają ZBUDOWANY plik wykonywalny,
//! podają komendy po stdin i sprawdzają linie JSON na stdout.
//!
//! Wszystko tutaj działa BEZ konta Spotify i bez sieci: sprawdzamy uzgodniony
//! kształt protokołu, odrzucanie błędnych wejść i zakończenie po utracie stdin.
//! Samo odtwarzanie na koncie NIE jest tu mierzone.

use std::io::{BufRead, BufReader, Write};
use std::process::{Child, ChildStdout, Command, Stdio};
use std::time::Duration;

const SESSION: &str = "spotifyLibrespot";

struct Host {
    child: Child,
    out: BufReader<ChildStdout>,
}

impl Host {
    fn start() -> Self {
        let child = Command::new(env!("CARGO_BIN_EXE_amc_spotify_librespot_host"))
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .expect("nie udało się uruchomić procesu pomocniczego");
        let out = BufReader::new(child.stdout.take().expect("stdout"));
        Self { child, out }
    }

    fn send(&mut self, line: &str) {
        let stdin = self.child.stdin.as_mut().expect("stdin");
        writeln!(stdin, "{line}").expect("zapis do stdin");
        stdin.flush().expect("flush stdin");
    }

    /// Czyta jedną linię stdout i parsuje ją jako JSON. Każda linia MUSI być JSON-em.
    fn recv(&mut self) -> serde_json::Value {
        let mut line = String::new();
        let read = self.out.read_line(&mut line).expect("odczyt stdout");
        assert!(read > 0, "proces zamknął stdout, a oczekiwano zdarzenia");
        serde_json::from_str(line.trim_end())
            .unwrap_or_else(|e| panic!("stdout musi być JSON lines, było {line:?} ({e})"))
    }

    fn close_stdin(&mut self) {
        drop(self.child.stdin.take());
    }

    /// Czeka na zakończenie procesu, zwraca kod wyjścia.
    fn wait(&mut self, limit: Duration) -> Option<i32> {
        let start = std::time::Instant::now();
        loop {
            match self.child.try_wait().expect("try_wait") {
                Some(status) => return status.code(),
                None if start.elapsed() > limit => {
                    let _ = self.child.kill();
                    return None;
                }
                None => std::thread::sleep(Duration::from_millis(50)),
            }
        }
    }
}

impl Drop for Host {
    fn drop(&mut self) {
        let _ = self.child.kill();
        let _ = self.child.wait();
    }
}

fn cmd(command: &str, request_id: i64, extra: serde_json::Value) -> String {
    let mut map = serde_json::Map::new();
    map.insert("command".into(), command.into());
    map.insert("requestId".into(), request_id.into());
    map.insert("sessionId".into(), SESSION.into());
    if let serde_json::Value::Object(o) = extra {
        for (k, v) in o {
            map.insert(k, v);
        }
    }
    serde_json::Value::Object(map).to_string()
}

#[test]
fn startup_announces_ready_with_protocol_version_and_nothing_about_login() {
    let mut host = Host::start();
    let ready = host.recv();
    assert_eq!(ready["type"], "ready");
    assert_eq!(ready["protocolVersion"], 1);
    assert_eq!(ready["sessionId"], SESSION);
    // ready NIE jest potwierdzeniem logowania.
    assert!(ready.get("authenticated").is_none());
    assert!(ready.get("loggedIn").is_none());
}

#[test]
fn ping_is_acked_with_matching_request_id() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("ping", 42, serde_json::json!({})));
    let ack = host.recv();
    assert_eq!(ack["type"], "ack");
    assert_eq!(ack["requestId"], 42);
    assert_eq!(ack["sessionId"], SESSION);
}

#[test]
fn devices_works_without_any_login() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("devices", 1, serde_json::json!({})));
    let reply = host.recv();
    // Na runnerze CI może nie być żadnej karty dźwiękowej - wtedy uczciwy błąd,
    // nigdy udawana lista. Oba wyniki są poprawne, byle w kontrakcie.
    match reply["type"].as_str() {
        Some("devices") => {
            assert_eq!(reply["requestId"], 1);
            let list = reply["devices"].as_array().expect("devices musi być tablicą");
            for d in list {
                assert!(d["name"].is_string(), "name musi być tekstem: {d}");
                assert!(d["isDefault"].is_boolean(), "isDefault musi być bool: {d}");
            }
        }
        Some("error") => {
            assert_eq!(reply["requestId"], 1);
            assert_eq!(reply["code"], "noAudioDevice");
            assert!(reply["message"].is_string());
        }
        other => panic!("nieoczekiwany typ odpowiedzi na devices: {other:?} ({reply})"),
    }
}

#[test]
fn unknown_session_id_is_refused() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(
        &serde_json::json!({"command":"ping","requestId":5,"sessionId":"spotifyWeb"}).to_string(),
    );
    let err = host.recv();
    assert_eq!(err["type"], "error");
    assert_eq!(err["requestId"], 5);
    assert_eq!(err["code"], "badSessionId");
}

#[test]
fn unknown_command_is_refused() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("teleport", 6, serde_json::json!({})));
    let err = host.recv();
    assert_eq!(err["type"], "error");
    assert_eq!(err["code"], "unknownCommand");
    assert_eq!(err["requestId"], 6);
}

#[test]
fn broken_json_is_refused_without_request_id() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send("{to nie jest json");
    let err = host.recv();
    assert_eq!(err["type"], "error");
    assert_eq!(err["code"], "badJson");
    assert!(err.get("requestId").is_none());
}

#[test]
fn bad_uri_and_out_of_range_numbers_are_refused() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");

    host.send(&cmd(
        "play",
        7,
        serde_json::json!({"uri":"spotify:album:4cOdK2wGLETKBW3PvgPWqT","positionMs":0,"playId":1}),
    ));
    let err = host.recv();
    assert_eq!(err["code"], "badUri");
    assert_eq!(err["requestId"], 7);

    host.send(&cmd("volume", 8, serde_json::json!({"volume":250})));
    let err = host.recv();
    assert_eq!(err["code"], "badRange");
    assert_eq!(err["requestId"], 8);

    host.send(&cmd("seek", 9, serde_json::json!({"positionMs":-1})));
    let err = host.recv();
    assert_eq!(err["code"], "badRange");
    assert_eq!(err["requestId"], 9);
}

#[test]
fn commands_before_initialize_are_refused_not_silently_ignored() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    for (i, (command, extra)) in [
        ("pause", serde_json::json!({})),
        ("resume", serde_json::json!({})),
        ("stop", serde_json::json!({})),
        ("seek", serde_json::json!({"positionMs":1000})),
        ("volume", serde_json::json!({"volume":50})),
        (
            "play",
            serde_json::json!({"uri":"spotify:track:4cOdK2wGLETKBW3PvgPWqT","positionMs":0,"playId":1}),
        ),
    ]
    .into_iter()
    .enumerate()
    {
        let rid = 100 + i as i64;
        host.send(&cmd(command, rid, extra));
        let err = host.recv();
        assert_eq!(err["type"], "error", "{command}: {err}");
        assert_eq!(err["code"], "notInitialized", "{command}: {err}");
        assert_eq!(err["requestId"], rid, "{command}: {err}");
    }
}

#[test]
fn speed_change_is_refused_instead_of_faked() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("speed", 11, serde_json::json!({"value":1.5})));
    let err = host.recv();
    assert_eq!(err["type"], "error");
    assert_eq!(err["code"], "speedControlUnsupported");
}

#[test]
fn access_token_is_never_echoed_back() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    // Bez sieci logowanie i tak się nie uda albo minie limit czasu; nas interesuje,
    // że w ŻADNEJ odpowiedzi nie ma tokenu. Dlatego celowo błędna nazwa urządzenia:
    // odrzucenie następuje przed jakąkolwiek próbą logowania.
    let secret = "BQ-TAJNY-TOKEN-TESTOWY-123";
    host.send(&cmd(
        "initialize",
        12,
        serde_json::json!({"accessToken":secret,"device":"urządzenie-którego-nie-ma","volume":40}),
    ));
    let reply = host.recv();
    assert_eq!(reply["type"], "error");
    assert_eq!(reply["code"], "noAudioDevice");
    let text = reply.to_string();
    assert!(!text.contains(secret), "token wyciekł do odpowiedzi: {text}");
}

#[test]
fn shutdown_is_acked_and_process_exits_cleanly() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("shutdown", 13, serde_json::json!({})));
    let ack = host.recv();
    assert_eq!(ack["type"], "ack");
    assert_eq!(ack["requestId"], 13);
    assert_eq!(
        host.wait(Duration::from_secs(20)),
        Some(0),
        "po shutdown proces ma wyjść kodem 0"
    );
}

#[test]
fn losing_stdin_terminates_the_process() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    host.send(&cmd("ping", 14, serde_json::json!({})));
    assert_eq!(host.recv()["type"], "ack");
    host.close_stdin();
    assert_eq!(
        host.wait(Duration::from_secs(20)),
        Some(0),
        "po utracie stdin proces ma się zakończyć i zwolnić audio"
    );
}

#[test]
fn every_stdout_line_is_json_only() {
    let mut host = Host::start();
    assert_eq!(host.recv()["type"], "ready");
    // Seria komend: każda odpowiedź musi być parsowalnym JSON-em (recv to wymusza).
    for (i, line) in [
        cmd("ping", 20, serde_json::json!({})),
        "nie json".to_string(),
        cmd("devices", 21, serde_json::json!({})),
        cmd("ping", 22, serde_json::json!({})),
    ]
    .into_iter()
    .enumerate()
    {
        host.send(&line);
        let reply = host.recv();
        assert!(
            reply.is_object(),
            "odpowiedź {i} nie jest obiektem JSON: {reply}"
        );
        assert!(reply["type"].is_string(), "brak pola type: {reply}");
        assert_eq!(reply["sessionId"], SESSION, "brak sessionId: {reply}");
    }
}
