"""REL-001: lokalny Sygnalista, prywatność i parytet odkrywania poleceń."""

from __future__ import annotations

from datetime import datetime, timezone
import os
import io
import json
from pathlib import Path
from urllib.error import HTTPError, URLError

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.problem_report import (
    MAX_REPORT_BYTES,
    DeliveryResult,
    DeliveryStatus,
    LocalOnlyTransport,
    PreparedReport,
    ReportDraft,
    ReportEnvironment,
    ReportKind,
    deliver_report,
    prepare_report,
    read_log_tail,
    redact_private_data,
    save_report_atomic,
)
from amc_wx_lite.problem_report_transport import (
    HttpReportTransport,
    configured_report_transport,
    transport_is_online,
)
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    command_presentation,
    resolve,
    SONOS_SUPPORTED_ACTIONS,
    SPOTIFY_SUPPORTED_ACTIONS,
    TIDAL_SUPPORTED_ACTIONS,
    WIIM_SUPPORTED_ACTIONS,
    describe,
    describe_action,
)


def _environment() -> ReportEnvironment:
    return ReportEnvironment(
        application_name="AMC Test",
        version="0.1.0",
        build="2026.10.19-abcdef0",
        channel="test",
        operating_system="Windows 11",
        active_session="Radio",
        components="Python 3.14; wxPython 4; host .NET 8",
    )


def test_private_values_are_removed_before_preview_copy_or_save() -> None:
    private = "\n".join((
        r"C:\Users\Jan\Nagrania\tajny-program.mp3",
        r"\\serwer\prywatne\sekret.wav",
        "file:///C:/Users/Jan/prywatny.txt",
        "https://example.test/private?token=sekret",
        "jan.kowalski@example.test",
        "192.168.10.52",
        "aa:bb:cc:dd:ee:ff",
        "123e4567-e89b-12d3-a456-426614174000",
        "Authorization: Bearer bardzo-tajny-token",
        '"access_token": "token-z-json"',
        "refresh_token=token-odswiezenia",
        "accountName: Jan Kowalski",
        "piosenka.mp3",
    ))

    safe = redact_private_data(private)

    for secret in (
        "Jan", "tajny-program.mp3", "sekret.wav", "prywatny.txt",
        "example.test", "jan.kowalski", "192.168.10.52",
        "aa:bb:cc:dd:ee:ff", "123e4567", "bardzo-tajny-token",
        "token-z-json", "token-odswiezenia", "Kowalski", "piosenka.mp3",
    ):
        assert secret not in safe
    assert "[ścieżka prywatna]" in safe
    assert "[wartość usunięta]" in safe


def test_report_uses_one_redacted_body_and_log_is_off_by_default() -> None:
    report = prepare_report(
        ReportDraft(
            ReportKind.BUG,
            "Problem z nagraniem.mp3",
            r"Plik C:\Users\Jan\Nagrania\audycja.mp3 nie działa",
        ),
        _environment(),
        log_lines=("Authorization: Bearer sekret",),
    )

    assert report.included_log is False
    assert "Dziennik diagnostyczny" not in report.body
    assert "sekret" not in report.body
    assert "Jan" not in report.body
    assert "Log dołączony: nie" in report.body
    assert report.report_id
    assert report.kind is ReportKind.BUG
    assert report.channel == "test"


def test_log_reader_is_bounded_and_redacted(tmp_path: Path) -> None:
    log = tmp_path / "frontend.log"
    log.write_text(
        "\n".join(
            f"wiersz {number}: plik-{number}.mp3"
            for number in range(300)
        ) + "\nAuthorization: Bearer ostatni-sekret\n",
        encoding="utf-8",
    )

    lines = read_log_tail(log)

    assert 1 <= len(lines) <= 200
    assert not any("ostatni-sekret" in line for line in lines)
    assert not any(".mp3" in line for line in lines)
    assert not any("wiersz 0:" in line for line in lines)


def test_atomic_save_writes_exact_preview_in_selected_profile_folder(
    tmp_path: Path,
) -> None:
    report = PreparedReport("[Błąd] Tytuł", "dokładny podgląd\n", False)
    target = save_report_atomic(
        report,
        tmp_path / "AMC Test" / "reports",
        moment=datetime(2026, 10, 19, 3, 4, 5, tzinfo=timezone.utc),
    )

    assert target.parent == tmp_path / "AMC Test" / "reports"
    assert target.name == "zgloszenie-20261019-030405.txt"
    assert target.read_text(encoding="utf-8") == report.body
    assert not list(target.parent.glob("*.tmp"))


class _Transport:
    def __init__(self, result) -> None:
        self.result = result
        self.calls: list[PreparedReport] = []

    def send(self, report: PreparedReport):
        self.calls.append(report)
        if isinstance(self.result, BaseException):
            raise self.result
        return self.result


def test_transport_contract_reports_success_error_offline_rate_and_size() -> None:
    report = PreparedReport("Tytuł", "treść\n", False)
    success = _Transport(DeliveryResult(DeliveryStatus.SUCCESS, 42, "https://issue"))
    failure = _Transport(DeliveryResult(DeliveryStatus.FAILED))
    offline = _Transport(ConnectionError("brak sieci"))
    rate = _Transport(DeliveryResult(DeliveryStatus.RATE_LIMITED))
    too_large = _Transport(DeliveryResult(DeliveryStatus.SUCCESS))

    assert deliver_report(success, report).status is DeliveryStatus.SUCCESS
    assert success.calls == [report]
    assert deliver_report(failure, report).status is DeliveryStatus.FAILED
    assert deliver_report(offline, report).status is DeliveryStatus.OFFLINE
    assert deliver_report(rate, report).status is DeliveryStatus.RATE_LIMITED
    oversized = PreparedReport("Tytuł", "x" * (MAX_REPORT_BYTES + 1), False)
    assert deliver_report(too_large, oversized).status is DeliveryStatus.TOO_LARGE
    assert too_large.calls == []
    assert LocalOnlyTransport().send(report).status is DeliveryStatus.NOT_CONFIGURED


class _Response:
    def __init__(self, status: int, payload: object) -> None:
        self.status = status
        self._body = io.BytesIO(json.dumps(payload).encode("utf-8"))

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return False

    def read(self, maximum: int) -> bytes:
        return self._body.read(maximum)

    def getcode(self) -> int:
        return self.status


def _network_report() -> PreparedReport:
    return prepare_report(
        ReportDraft(ReportKind.SUGGESTION, "Tytuł", "Opis"),
        _environment(),
        report_id="123e4567-e89b-42d3-a456-426614174000",
    )


def test_https_transport_sends_exact_preview_without_any_secret() -> None:
    calls = []

    def opener(request, *, timeout):
        calls.append((request, timeout))
        return _Response(201, {
            "issueNumber": 84,
            "issueUrl": "https://github.com/michalkasperczak/AMC/issues/84",
        })

    report = _network_report()
    result = HttpReportTransport(
        "https://reports.example.test/v1/report",
        opener=opener,
    ).send(report)

    assert result == DeliveryResult(
        DeliveryStatus.SUCCESS,
        84,
        "https://github.com/michalkasperczak/AMC/issues/84",
    )
    request, timeout = calls[0]
    assert request.full_url == "https://reports.example.test/v1/report"
    assert timeout == 15.0
    payload = json.loads(request.data.decode("utf-8"))
    assert payload == {
        "schemaVersion": 1,
        "reportId": report.report_id,
        "kind": "suggestion",
        "title": report.title,
        "reportText": report.body,
        "includedLog": False,
        "channel": "test",
    }
    headers = {key.casefold(): value for key, value in request.header_items()}
    assert "authorization" not in headers
    assert "token" not in request.data.decode("utf-8").casefold()


def test_https_transport_maps_network_rate_size_and_malformed_responses() -> None:
    report = _network_report()

    def http_error(code: int):
        def opener(request, *, timeout):
            del request, timeout
            raise HTTPError("https://reports.example.test/v1/report", code, "error", {}, None)
        return opener

    def offline(request, *, timeout):
        del request, timeout
        raise URLError("offline")

    assert HttpReportTransport(
        "https://reports.example.test/v1/report", opener=http_error(429)
    ).send(report).status is DeliveryStatus.RATE_LIMITED
    assert HttpReportTransport(
        "https://reports.example.test/v1/report", opener=http_error(413)
    ).send(report).status is DeliveryStatus.TOO_LARGE
    assert HttpReportTransport(
        "https://reports.example.test/v1/report", opener=offline
    ).send(report).status is DeliveryStatus.OFFLINE
    assert HttpReportTransport(
        "https://reports.example.test/v1/report",
        opener=lambda request, timeout: _Response(200, {}),
    ).send(report).status is DeliveryStatus.FAILED
    assert HttpReportTransport(
        "https://reports.example.test/v1/report",
        opener=lambda request, timeout: _Response(201, {"issueNumber": "x"}),
    ).send(report).status is DeliveryStatus.FAILED


def test_transport_configuration_accepts_only_plain_https_endpoint(tmp_path: Path) -> None:
    absent = configured_report_transport(environment={}, resource_path=tmp_path / "none")
    assert isinstance(absent, LocalOnlyTransport)
    for unsafe in (
        "http://reports.example.test/v1/report",
        "https://user:secret@reports.example.test/v1/report",
        "https://reports.example.test/v1/report?token=secret",
        "https://reports.example.test/v1/report#fragment",
    ):
        assert isinstance(
            configured_report_transport(
                environment={"AMC_PROBLEM_REPORT_ENDPOINT": unsafe},
                resource_path=tmp_path / "none",
            ),
            LocalOnlyTransport,
        )
    online = configured_report_transport(
        environment={
            "AMC_PROBLEM_REPORT_ENDPOINT": "https://reports.example.test/v1/report"
        },
        resource_path=tmp_path / "none",
    )
    assert transport_is_online(online)


def test_report_command_has_one_name_in_menu_palette_help_and_dispatch() -> None:
    action = Action.REPORT_PROBLEM
    for session in SessionId:
        entries = {entry.action: entry for entry in entries_for_session(session)}
        assert entries[action].name == "Zgłoś błąd lub sugestię…"
        assert entries[action].shortcut == "Ctrl+Alt+Z"
    assert describe_action(action) == "Zgłoś błąd lub sugestię"
    assert ("Ctrl+Alt+Z", "Zgłoś błąd lub sugestię") in describe()
    presentation = command_presentation(action)
    assert presentation is not None
    assert presentation.shortcut == "Ctrl+Alt+Z"
    for player_view in (False, True):
        assert resolve(Chord("Z", ctrl=True, alt=True), player_view=player_view, radio_session=False) is action
    gui_source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    assert "elif action is Action.REPORT_PROBLEM:" in gui_source
    assert "self._show_problem_report()" in gui_source
    for supported in (
        TIDAL_SUPPORTED_ACTIONS,
        SPOTIFY_SUPPORTED_ACTIONS,
        WIIM_SUPPORTED_ACTIONS,
        SONOS_SUPPORTED_ACTIONS,
    ):
        assert action in supported


def test_report_menu_help_and_shortcut_match_in_both_profiles() -> None:
    previous = os.environ.get("AMC_RELEASE_PROFILE")
    try:
        for profile in (TEST_PROFILE, PUBLIC_PROFILE):
            os.environ["AMC_RELEASE_PROFILE"] = profile.profile_id
            for session in SessionId:
                if session.value not in profile.session_ids:
                    continue
                help_items = [
                    item for menu in menus_for_session(session)
                    for item in menu.items
                    if item.action is Action.REPORT_PROBLEM
                ]
                assert len(help_items) == 1
                assert help_items[0].shortcut == "Ctrl+Alt+Z"
                palette = {
                    entry.action: entry for entry in entries_for_session(session)
                }
                assert palette[Action.REPORT_PROBLEM].shortcut == "Ctrl+Alt+Z"
                for player_view in (False, True):
                    assert resolve(
                        Chord("Z", ctrl=True, alt=True),
                        player_view=player_view,
                        radio_session=session is SessionId.RADIO,
                    ) is Action.REPORT_PROBLEM
    finally:
        if previous is None:
            os.environ.pop("AMC_RELEASE_PROFILE", None)
        else:
            os.environ["AMC_RELEASE_PROFILE"] = previous


def test_both_profiles_advertise_the_shared_report_command_without_secrets() -> None:
    assert "problem-report-local" in TEST_PROFILE.capabilities
    assert "problem-report-local" in PUBLIC_PROFILE.capabilities
    assert not any("github" in value or "worker" in value for value in PUBLIC_PROFILE.capabilities)


def test_dialog_source_keeps_log_opt_in_full_preview_and_initial_focus() -> None:
    source = (
        Path(__file__).parents[1]
        / "amc_wx_lite"
        / "problem_report_dialog.py"
    ).read_text(encoding="utf-8")

    assert "self.include_log.SetValue(False)" in source
    assert "Pełny podgląd zredagowanego raportu" in source
    assert "wx.CallAfter(self.subject.SetFocus)" in source
    assert "Wysyłanie przez Internet nie jest jeszcze skonfigurowane" in source
    assert 'label="Wyślij zgłoszenie"' in source
    assert 'self._announce("Wysyłanie zgłoszenia")' in source
    assert "BackgroundRunner" in source
    assert "Skopiuj adres zgłoszenia" in source
    assert "path.parent" not in source
