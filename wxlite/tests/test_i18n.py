"""Kompletność języków i wspólny kontrakt menu, pomocy oraz palety."""

from __future__ import annotations

import json
import ast
import re
import unittest
from pathlib import Path

from amc_wx_lite import i18n, menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, describe_action
from amc_wx_lite.state_store import StateStore
from amc_wx_lite.transport_parity import (
    DEFAULT_TEMPLATES_BY_LANGUAGE,
    TEMPLATE_KEYS,
    TEMPLATE_LABELS_BY_LANGUAGE,
    MessagePolicy,
)
from tools import translation_inventory


def test_ui_catalogs_have_exactly_the_same_nonempty_keys() -> None:
    catalogs = i18n.catalogs()
    assert set(catalogs) == {i18n.POLISH, i18n.ENGLISH}
    polish_keys = set(catalogs[i18n.POLISH])
    assert polish_keys
    for language, catalog in catalogs.items():
        assert set(catalog) == polish_keys, language
        assert all(isinstance(value, str) and value for value in catalog.values())


def test_podcast_manual_refresh_setting_has_english_control_labels() -> None:
    try:
        i18n.configure("en")
        assert i18n.ui_text(
            "Automatyczny przebieg co &ile minut (0 = tylko na żądanie):"
        ) == "Automatic run every how many &minutes (0 = on demand only):"
        assert i18n.ui_text(
            "Odstęp automatycznych przebiegów podcastów i YouTube w minutach; "
            "zero oznacza tylko ręczne odświeżanie"
        ) == (
            "Automatic podcast and YouTube run interval in minutes; "
            "zero means manual refresh only"
        )
        assert "manual refresh only" in i18n.command_description(
            "settings.general", "Ustawienia programu"
        )
    finally:
        i18n.configure("pl")


def test_system_language_resolution_and_preference_validation() -> None:
    assert i18n.resolve_language("system", system_locale="pl_PL") == "pl"
    assert i18n.resolve_language("system", system_locale="Polish_Poland") == "pl"
    assert i18n.resolve_language("system", system_locale="Polish (Poland)") == "pl"
    assert i18n.resolve_language("system", system_locale="en_US") == "en"
    assert i18n.resolve_language("system", system_locale="de_DE") == "en"
    assert i18n.resolve_language("pl", system_locale="en_US") == "pl"
    assert i18n.resolve_language("EN", system_locale="pl_PL") == "en"
    assert i18n.normalize_preference("technical-id") == "system"


def test_message_catalogs_have_matching_keys_and_placeholders() -> None:
    assert set(DEFAULT_TEMPLATES_BY_LANGUAGE) == {"pl", "en"}
    assert set(TEMPLATE_LABELS_BY_LANGUAGE) == {"pl", "en"}
    expected_defaults = set(DEFAULT_TEMPLATES_BY_LANGUAGE["pl"])
    for templates in DEFAULT_TEMPLATES_BY_LANGUAGE.values():
        assert set(templates) == expected_defaults
    for labels in TEMPLATE_LABELS_BY_LANGUAGE.values():
        assert tuple(labels) == TEMPLATE_KEYS
        assert all(labels.values())
    assert MessagePolicy(language="pl").render(
        "favorite.added", item="Utwór"
    ) == "Dodano do ulubionych: Utwór"
    assert MessagePolicy(language="en").render(
        "favorite.added", item="Track"
    ) == "Added to favorites: Track"


def test_legacy_custom_messages_migrate_only_to_polish() -> None:
    store = StateStore("unused")
    state = store.load_payload({
        "options": {
            "message_templates": {"queue.added": "Moja kolejka: {item}"}
        }
    })
    assert state.options.message_templates_by_language == {
        "pl": {"queue.added": "Moja kolejka: {item}"}
    }
    payload = store.to_payload(state)
    assert "message_templates" not in payload["options"]
    assert payload["options"]["message_templates_by_language"] == {
        "pl": {"queue.added": "Moja kolejka: {item}"}
    }


def test_english_menu_help_and_palette_share_stable_actions_and_shortcuts() -> None:
    try:
        i18n.configure("en")
        for session in SessionId:
            menus = menu_model.menus_for_session(
                session, youtube_account_available=True
            )
            menu_items = {
                item.action: item
                for menu in menus
                for item in menu.items
                if item.action is not None and not item.is_separator
            }
            palette = {
                entry.action: entry
                for entry in entries_for_session(
                    session, youtube_account_available=True
                )
            }
            assert set(menu_items) == set(palette)
            for action, item in menu_items.items():
                name = palette[action].name
                help_text = describe_action(action, i18n.english_action_name(session.value))
                assert name
                assert help_text
                assert action.value not in name
                assert "Action." not in name
                assert item.shortcut == palette[action].shortcut or (
                    item.shortcut is not None and palette[action].shortcut is not None
                )
    finally:
        i18n.configure("pl")


def test_language_change_does_not_change_shortcut_model() -> None:
    try:
        i18n.configure("pl")
        polish = {
            item.action: item.shortcut
            for menu in menu_model.menus_for_session(SessionId.FILES)
            for item in menu.items
            if item.action is not None
        }
        i18n.configure("en")
        english = {
            item.action: item.shortcut
            for menu in menu_model.menus_for_session(SessionId.FILES)
            for item in menu.items
            if item.action is not None
        }
        assert english == polish
    finally:
        i18n.configure("pl")


def test_window_tree_does_not_translate_user_items_without_opt_in() -> None:
    class FakeWindow:
        def __init__(self) -> None:
            self.label = "Gotowe"
            self.name = "Zapisz ustawienia"
            self.help_text = "Zmiana języka zostanie zastosowana po zapisaniu ustawień."
            self.items = ["Gotowe", "Moja prywatna stacja"]

        def GetLabel(self) -> str:
            return self.label

        def SetLabel(self, value: str) -> None:
            self.label = value

        def GetName(self) -> str:
            return self.name

        def SetName(self, value: str) -> None:
            self.name = value

        def GetHelpText(self) -> str:
            return self.help_text

        def SetHelpText(self, value: str) -> None:
            self.help_text = value

        def GetCount(self) -> int:
            return len(self.items)

        def GetString(self, index: int) -> str:
            return self.items[index]

        def SetString(self, index: int, value: str) -> None:
            self.items[index] = value

        def GetChildren(self) -> tuple[object, ...]:
            return ()

    try:
        i18n.configure("pl")
        window = FakeWindow()
        assert i18n.reconfigure_window_tree(window, "en") == "en"
        assert window.label == "Ready"
        assert window.name == "Save settings"
        assert window.help_text == (
            "The language change is applied after you save the settings."
        )
        assert window.items == ["Gotowe", "Moja prywatna stacja"]

        i18n.localize_window_tree(window, translate_items=True)
        assert window.items == ["Ready", "Moja prywatna stacja"]

        assert i18n.reconfigure_window_tree(window, "pl") == "pl"
        assert window.label == "Gotowe"
        assert window.name == "Zapisz ustawienia"
        assert window.help_text == (
            "Zmiana języka zostanie zastosowana po zapisaniu ustawień."
        )
        # Lista nie jest dotykana przez zmianę języka. Pierwszy element został
        # wcześniej przetłumaczony tylko przez jawne opt-in testu.
        assert window.items == ["Ready", "Moja prywatna stacja"]
    finally:
        i18n.configure("pl")


def test_open_window_dynamic_text_can_switch_back_to_polish() -> None:
    try:
        i18n.configure("en")
        assert i18n.ui_text("Ulubione") == "Favorites"
    finally:
        i18n.configure("pl")
    assert i18n.translate_ui_text(
        "Added bookmark 2: 01:30",
        source_language="en",
        target_language="pl",
    ) == "Dodano zakładkę 2: 01:30"


def test_english_to_polish_skips_text_values_and_deleting_windows() -> None:
    class TextEntry:
        def __init__(self) -> None:
            self.value = "Added to favorites: {item}"
            self.name = "Save settings"
            self.help_text = (
                "The language change is applied after you save the settings."
            )

        def GetValue(self) -> str:
            return self.value

        def ChangeValue(self, value: str) -> None:
            self.value = value

        def GetLabel(self) -> str:
            return self.value

        def SetLabel(self, _value: str) -> None:
            raise AssertionError("SetLabel must never be used for a text entry")

        def GetName(self) -> str:
            return self.name

        def SetName(self, value: str) -> None:
            self.name = value

        def GetHelpText(self) -> str:
            return self.help_text

        def SetHelpText(self, value: str) -> None:
            self.help_text = value

        def GetChildren(self) -> tuple[object, ...]:
            return ()

    class DeletingWindow:
        def IsBeingDeleted(self) -> bool:
            return True

        def GetLabel(self) -> str:
            raise AssertionError("a deleting window must not be inspected")

    class Root:
        def __init__(self, children: tuple[object, ...]) -> None:
            self.label = "Ready"
            self.children = children

        def GetLabel(self) -> str:
            return self.label

        def SetLabel(self, value: str) -> None:
            self.label = value

        def GetChildren(self) -> tuple[object, ...]:
            return self.children

    try:
        i18n.configure("en")
        text_entry = TextEntry()
        root = Root((text_entry, DeletingWindow()))
        assert i18n.reconfigure_window_tree(root, "pl") == "pl"
        assert root.label == "Gotowe"
        assert text_entry.GetValue() == "Added to favorites: {item}"
        assert text_entry.GetName() == "Zapisz ustawienia"
        assert text_entry.GetHelpText() == (
            "Zmiana języka zostanie zastosowana po zapisaniu ustawień."
        )
    finally:
        i18n.configure("pl")


def test_native_english_to_polish_switch_survives_destroyed_settings_dialog() -> None:
    try:
        import wx
    except ImportError:
        raise unittest.SkipTest("test wymaga prawdziwego wxPython") from None
    if not getattr(wx, "version", None):
        raise unittest.SkipTest("test wymaga prawdziwego wxPython")

    app = wx.GetApp() or wx.App(False)
    frame = wx.Frame(None)
    menu_bar = wx.MenuBar()
    menu_bar.Append(wx.Menu(), "&File")
    frame.SetMenuBar(menu_bar)
    panel = wx.Panel(frame)
    message_template = wx.TextCtrl(panel, value="Added to favorites: {item}")
    message_template.SetName("Save settings")
    message_template.SetHelpText(
        "The language change is applied after you save the settings."
    )
    closing_settings = wx.Dialog(frame)
    wx.TextCtrl(closing_settings, value="Added to favorites: {item}")
    closing_settings.Destroy()
    try:
        # On Windows wx keeps the destroyed modal in the parent's child tree
        # for this turn and does not yet report IsBeingDeleted().  This is the
        # exact state that used to expose its TextCtrl to SetLabel().
        assert not closing_settings.IsBeingDeleted()
        assert closing_settings in frame.GetChildren()
        i18n.configure("en")
        assert i18n.reconfigure_window_tree(frame, "pl") == "pl"
        # This is the next operation in the real save handler.  The regression
        # used to abort before this native menu rebuild could run.
        file_menu = menu_model.menus_for_session(SessionId.FILES)[0].title
        menu_bar.SetMenuLabel(0, file_menu)
        assert menu_bar.GetMenuLabel(0) == "&Pliki"
        assert message_template.GetValue() == "Added to favorites: {item}"
        assert message_template.GetName() == "Zapisz ustawienia"
    finally:
        i18n.configure("pl")
        frame.Destroy()
        app.Yield()


def test_future_language_template_is_valid_and_covers_core_sections() -> None:
    path = (
        Path(__file__).resolve().parents[1]
        / "amc_wx_lite"
        / "locales"
        / "template.json"
    )
    template = json.loads(path.read_text(encoding="utf-8"))
    assert set(i18n.catalogs()["pl"]) <= set(template["ui"])
    assert set(TEMPLATE_KEYS) == set(template["messageTemplateLabels"])
    assert set(DEFAULT_TEMPLATES_BY_LANGUAGE["pl"]) == set(
        template["messageTemplates"]
    )
    legacy = i18n.legacy_english_catalog()
    assert set(template["legacyUi"]) == set(legacy["static"])
    assert set(template["legacyTemplates"]) == set(legacy["templates"])


def test_every_inventoried_wxpython_text_has_complete_english_catalog() -> None:
    inventory = translation_inventory.inventory_files(
        translation_inventory.default_source_paths()
    )
    legacy = i18n.legacy_english_catalog()
    exact_sources = set(i18n._UI_TEXT_EN) | set(legacy["static"])  # noqa: SLF001
    assert inventory.exact <= exact_sources
    assert inventory.templates == set(legacy["templates"])
    assert all(legacy["static"].values())
    assert all(legacy["templates"].values())

    placeholder = re.compile(r"\{arg\d+\}")
    for source, target in legacy["templates"].items():
        assert set(placeholder.findall(source)) == set(placeholder.findall(target))
    serialized = json.dumps(legacy, ensure_ascii=False)
    assert "ZXQAMC" not in serialized
    assert "Action." not in serialized
    assert "stationName" not in legacy["static"]


def test_dynamic_english_messages_preserve_runtime_values() -> None:
    try:
        i18n.configure("en")
        assert i18n.ui_text(
            "Nie można teraz odczytać stanu konta Spotify."
        ) == "The Spotify account status cannot be read right now."
        assert i18n.ui_text(
            "Dodano zakładkę 2: 01:30"
        ) == "Added bookmark 2: 01:30"
        assert i18n.ui_text(
            "Zakończono zaplanowane nagrywanie Radio Jazz. "
            "Zapisano plików: 3"
        ) == (
            "Finished scheduled recording of Radio Jazz. Files saved: 3"
        )
        # Import radia: zdania tłumaczone osobno, a nie „Added nowe stacje”.
        assert i18n.ui_text("Dodano nowe stacje: 0") == "New stations added: 0"
        assert i18n.ui_text("Uzupełniono nazwy: 27") == "Station names filled in: 27"
        assert i18n.ui_text("Pominięto: 3") == "Skipped: 3"
        assert i18n.ui_text(
            "Playlista nie zawiera nowych prawidłowych stacji"
        ) == "The playlist contains no new valid stations"
    finally:
        i18n.configure("pl")


def test_settings_tabs_and_playback_bookmarks_use_distinct_terminology() -> None:
    assert i18n.text_for("pl", "settings.tab.general") == "Ogólne"
    assert i18n.text_for("en", "settings.tab.general") == "General"

    legacy = i18n.legacy_english_catalog()
    bookmark_entries = {
        source: target
        for catalog in (legacy["static"], legacy["templates"])
        for source, target in catalog.items()
        if "zakładk" in source.casefold()
    }
    assert bookmark_entries
    assert all("bookmark" in target.casefold() for target in bookmark_entries.values())
    assert all(
        re.search(r"\btabs?\b", target, re.IGNORECASE) is None
        for target in bookmark_entries.values()
    )


def test_every_static_settings_control_text_has_an_english_translation() -> None:
    gui_path = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    tree = ast.parse(gui_path.read_text(encoding="utf-8"))
    dialog = next(
        node
        for node in tree.body
        if isinstance(node, ast.ClassDef)
        and node.name == "GeneralPlaybackOptionsDialog"
    )
    text_methods = {"SetName", "SetHelpText", "SetLabel", "ChangeValue", "MessageBox"}
    static_texts: set[str] = set()
    for node in ast.walk(dialog):
        if not isinstance(node, ast.Call):
            continue
        if isinstance(node.func, ast.Attribute):
            name = node.func.attr
        elif isinstance(node.func, ast.Name):
            name = node.func.id
        else:
            name = ""
        if name not in text_methods and not any(
            keyword.arg in {"label", "title", "message", "choices", "value"}
            for keyword in node.keywords
        ):
            continue
        for value in (*node.args, *(item.value for item in node.keywords)):
            if (
                isinstance(value, ast.Constant)
                and isinstance(value.value, str)
                and len(value.value) > 1
            ):
                static_texts.add(value.value)
    assert static_texts <= set(i18n._UI_TEXT_EN)  # noqa: SLF001
