"""YT-001: konto Google, bezpieczne etykiety i jeden model poleceń."""

from __future__ import annotations

from pathlib import Path

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite import menu_model
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import Action, describe
from amc_wx_lite.youtube_account import (
    CLIENT_ID_ENVIRONMENT,
    CLIENT_SECRET_FILE_ENVIRONMENT,
    PUBLIC_CLIENT_ID_ENVIRONMENT,
    TOKEN_BROKER_ENVIRONMENT,
    account_connected,
    capability_available,
    playlist_sync_preview,
    resolve_youtube_client_id,
    resolve_youtube_client_secret_file,
    resolve_youtube_token_broker,
    safe_message,
    sync_preview,
)


CLIENT_ID = "123456789012-example.apps.googleusercontent.com"
CLIENT_SECRET = "syntetyczny-sekret-klienta"
TOKEN_BROKER = "https://oauth.example.test/v1/google/token"


def test_oauth_configuration_is_profile_specific_and_validated(tmp_path: Path) -> None:
    assert resolve_youtube_client_id(
        {CLIENT_ID_ENVIRONMENT: CLIENT_ID}, profile=TEST_PROFILE
    ) == CLIENT_ID
    assert resolve_youtube_client_id(
        {PUBLIC_CLIENT_ID_ENVIRONMENT: CLIENT_ID}, profile=PUBLIC_PROFILE
    ) == CLIENT_ID
    assert resolve_youtube_client_id(
        {CLIENT_ID_ENVIRONMENT: CLIENT_ID},
        profile=PUBLIC_PROFILE,
        resource_path=tmp_path / "brak-publicznego-id.txt",
    ) is None
    assert resolve_youtube_client_id(
        {CLIENT_ID_ENVIRONMENT: "niedozwolony identyfikator"},
        profile=TEST_PROFILE,
        resource_path=tmp_path / "brak.txt",
    ) is None

    resource = tmp_path / "youtube-oauth-client-id.txt"
    resource.write_text(CLIENT_ID + "\n", encoding="utf-8")
    assert resolve_youtube_client_id(
        {}, profile=TEST_PROFILE, resource_path=resource
    ) == CLIENT_ID

    secret = tmp_path / "youtube-oauth-client-secret.txt"
    secret.write_text(CLIENT_SECRET + "\n", encoding="utf-8")
    assert resolve_youtube_client_secret_file(
        {}, profile=TEST_PROFILE, resource_path=secret
    ) == secret.resolve()
    assert resolve_youtube_client_secret_file(
        {CLIENT_SECRET_FILE_ENVIRONMENT: str(secret)}, profile=TEST_PROFILE
    ) == secret.resolve()
    assert resolve_youtube_client_secret_file(
        {CLIENT_SECRET_FILE_ENVIRONMENT: str(secret)}, profile=PUBLIC_PROFILE
    ) is None

    secret.write_text("sekret ze spacją\n", encoding="utf-8")
    assert resolve_youtube_client_secret_file(
        {}, profile=TEST_PROFILE, resource_path=secret
    ) is None

    broker = tmp_path / "youtube-oauth-public-token-broker.txt"
    broker.write_text(TOKEN_BROKER + "\n", encoding="utf-8")
    assert resolve_youtube_token_broker(
        {}, profile=PUBLIC_PROFILE, resource_path=broker
    ) == TOKEN_BROKER
    assert resolve_youtube_token_broker(
        {TOKEN_BROKER_ENVIRONMENT: TOKEN_BROKER}, profile=PUBLIC_PROFILE
    ) == TOKEN_BROKER
    assert resolve_youtube_token_broker(
        {TOKEN_BROKER_ENVIRONMENT: TOKEN_BROKER}, profile=TEST_PROFILE
    ) is None
    assert resolve_youtube_token_broker(
        {TOKEN_BROKER_ENVIRONMENT: "http://oauth.example.test/v1/google/token"},
        profile=PUBLIC_PROFILE,
        resource_path=tmp_path / "brak-brokera.txt",
    ) is None


def test_public_profile_keeps_google_account_unavailable(
    tmp_path: Path,
) -> None:
    """Brak domeny i wdrożenia nie może tworzyć martwej funkcji ani psuć hosta."""

    assert resolve_youtube_client_id(
        {},
        profile=PUBLIC_PROFILE,
        resource_path=tmp_path / "brak-publicznego-id.txt",
    ) is None
    assert resolve_youtube_client_secret_file(
        {},
        profile=PUBLIC_PROFILE,
        resource_path=tmp_path / "brak-sekretu.txt",
    ) is None
    assert resolve_youtube_token_broker(
        {},
        profile=PUBLIC_PROFILE,
        resource_path=tmp_path / "brak-brokera.txt",
    ) is None

    client = LiteHostClient(Path("host.exe"), release_profile="public")
    command = client._command()
    assert "--release-profile" in command
    assert "--youtube-client-id" not in command
    assert "--youtube-client-secret-file" not in command
    assert "--youtube-token-broker" not in command

    assert not any(
        item.action is Action.YOUTUBE_ACCOUNT
        for menu in menu_model.menus_for_session(SessionId.PODCASTS)
        for item in menu.items
    )
    assert not any(
        item.action is Action.YOUTUBE_ACCOUNT
        for item in entries_for_session(SessionId.PODCASTS)
    )


def test_protocol_payload_never_becomes_object_representation() -> None:
    payload = {
        "connected": True,
        "message": "  YouTube:\n zalogowano  ",
        "channelLabels": [
            " Kanał\r\nPierwszy ",
            {"title": "rekord techniczny"},
            "Kanał Drugi",
        ],
        "additions": 2,
        "restores": 0,
        "alreadyPresent": 4,
        "suppressed": 1,
        "channelId": "UC-TAJNY-TECHNICZNY",
    }
    preview = sync_preview(payload)

    assert account_connected(payload) is True
    assert safe_message(payload, "brak") == "YouTube: zalogowano"
    assert preview is not None
    assert preview.labels == ("Kanał Pierwszy", "Kanał Drugi")
    assert preview.additions == 2 and preview.already_present == 4
    assert "UC-TAJNY" not in " ".join(preview.labels)
    assert safe_message(object(), "Bezpieczny komunikat") == "Bezpieczny komunikat"

    playlist = playlist_sync_preview(
        {
            "playlistLabels": [
                " Muzyka\r\nspokojna; prywatna; 12 materiałów ",
                {"playlistId": "PL-TAJNY"},
            ],
            "additions": 1,
            "updates": 2,
            "truncated": 1,
            "playlistId": "PL-TAJNY",
        }
    )
    assert playlist is not None
    assert playlist.labels == ("Muzyka spokojna; prywatna; 12 materiałów",)
    assert playlist.additions == 1 and playlist.updates == 2
    assert playlist.truncated == 1
    assert "PL-TAJNY" not in " ".join(playlist.labels)


def test_pascal_case_payload_from_real_dotnet_host_is_supported() -> None:
    capabilities = {
        "Available": True,
        "ReadOnly": True,
        "SeparateProfileCredentials": True,
        "Message": "Konto YouTube jest dostępne tylko do odczytu.",
    }
    status = {"Connected": True, "Message": "YouTube: zalogowano"}
    preview_payload = {
        "Additions": 2,
        "Restores": 1,
        "AlreadyPresent": 4,
        "Suppressed": 3,
        "ChannelLabels": ["Kanał Pierwszy", "Kanał Drugi"],
        "Message": "Sprawdzono subskrypcje YouTube.",
    }

    assert capability_available(capabilities) is True
    assert safe_message(capabilities, "brak") == (
        "Konto YouTube jest dostępne tylko do odczytu."
    )
    assert account_connected(status) is True
    assert safe_message(status, "brak") == "YouTube: zalogowano"
    preview = sync_preview(preview_payload)
    assert preview is not None
    assert preview.additions == 2
    assert preview.restores == 1
    assert preview.already_present == 4
    assert preview.suppressed == 3
    assert preview.labels == ("Kanał Pierwszy", "Kanał Drugi")


def test_host_client_passes_secret_path_not_secret_value() -> None:
    secret_file = Path("lokalna-konfiguracja") / "youtube-secret.txt"
    client = LiteHostClient(
        Path("host.exe"),
        youtube_client_id=CLIENT_ID,
        youtube_client_secret_file=secret_file,
    )
    command = client._command()
    assert command[-4:] == [
        "--youtube-client-id",
        CLIENT_ID,
        "--youtube-client-secret-file",
        str(secret_file),
    ]
    assert CLIENT_SECRET not in command

    public_client = LiteHostClient(
        Path("host.exe"),
        youtube_client_id=CLIENT_ID,
        youtube_token_broker=TOKEN_BROKER,
    )
    public_command = public_client._command()
    assert public_command[-4:] == [
        "--youtube-client-id",
        CLIENT_ID,
        "--youtube-token-broker",
        TOKEN_BROKER,
    ]
    assert "--youtube-client-secret-file" not in public_command

    calls: list[tuple[str, object, float]] = []

    def call(operation: str, args: object = None, timeout: float = 10.0) -> dict:
        calls.append((operation, args, timeout))
        return {"message": "ok"}

    client.call = call  # type: ignore[method-assign]
    client.youtube_account_capabilities()
    client.youtube_account_status()
    client.youtube_account_login()
    client.youtube_account_cancel_login()
    client.youtube_account_logout()
    client.youtube_subscription_sync_preview(restore_suppressed=True)
    client.youtube_subscription_sync_apply(restore_suppressed=False)
    client.youtube_playlist_sync_preview(restore_suppressed=True)
    client.youtube_playlist_sync_apply(restore_suppressed=False)

    assert [entry[0] for entry in calls] == [
        "youtube.accountCapabilities",
        "youtube.accountStatus",
        "youtube.accountLogin",
        "youtube.accountCancelLogin",
        "youtube.accountLogout",
        "youtube.subscriptionSyncPreview",
        "youtube.subscriptionSyncApply",
        "youtube.playlistSyncPreview",
        "youtube.playlistSyncApply",
    ]
    assert calls[-4][1] == {"restoreSuppressed": True}
    assert calls[-3][1] == {"restoreSuppressed": False}
    assert calls[-2][1] == {"restoreSuppressed": True}
    assert calls[-1][1] == {"restoreSuppressed": False}
    assert calls[2][2] == 960.0


def test_runtime_capability_controls_menu_palette_and_help_together() -> None:
    host_confirmed = capability_available({"Available": True})
    hidden = menu_model.menus_for_session(SessionId.PODCASTS)
    visible = menu_model.menus_for_session(
        SessionId.PODCASTS,
        youtube_account_available=host_confirmed,
    )
    assert not any(
        item.action is Action.YOUTUBE_ACCOUNT
        for menu in hidden
        for item in menu.items
    )
    assert sum(
        item.action is Action.YOUTUBE_ACCOUNT
        for menu in visible
        for item in menu.items
    ) == 1
    assert not any(
        item.action is Action.YOUTUBE_ACCOUNT
        for item in entries_for_session(SessionId.PODCASTS)
    )
    entry = next(
        item
        for item in entries_for_session(
            SessionId.PODCASTS,
            youtube_account_available=host_confirmed,
        )
        if item.action is Action.YOUTUBE_ACCOUNT
    )
    assert entry.name == "Konto YouTube, subskrypcje i playlisty…"
    assert dict(describe("Podcasty i YouTube", {Action.YOUTUBE_ACCOUNT})) == {
        "Bez skrótu": "Konto YouTube, subskrypcje i playlisty"
    }


def test_dialog_source_has_intentional_accessible_labels() -> None:
    source = (Path(__file__).parents[1] / "amc_wx_lite" / "gui.py").read_text(
        encoding="utf-8"
    )
    dialog = source[
        source.index("class YouTubeAccountDialog") :
        source.index("class SonosAccountDialog")
    ]
    for label in (
        "Stan konta YouTube",
        "Kanały planowane do zapisania w Bibliotece AMC",
        "Playlisty planowane do zapisania w Bibliotece AMC",
        "Sprawdź playlisty YouTube bez zapisywania",
        "Zapisz sprawdzone playlisty w Bibliotece AMC",
        "Przywróć kanały i playlisty wcześniej usunięte z Biblioteki AMC",
        "Zaloguj konto YouTube przez Google w przeglądarce",
        "Cofnij dostęp Google i usuń dane konta z AMC",
        "Cofnij dostęp Google do YouTube i usuń dane konta z AMC",
        "Wyloguj konto YouTube tylko w AMC",
        "Anuluj oczekiwanie na logowanie YouTube",
        "Zamknij okno konta YouTube",
        "AMC będzie czekać do 15 minut",
        "uruchomienie AMC ani logowanie nie przywraca usuniętych kanałów",
        "Przywracanie wcześniej usuniętych kanałów wymaga",
    ):
        assert label in dialog, f"Brak dostępnej etykiety okna YouTube: {label}"
    assert "self.close_button.Bind(wx.EVT_BUTTON, self._on_close_button)" in dialog
    assert "self._close_after_cancel = True" in dialog
    assert "str(payload)" not in dialog
    assert "repr(" not in dialog
    assert "Nie usuwa to danych zapisanych w YouTube ani publicznych" in source
