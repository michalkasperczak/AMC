"""Structural contract for the native, keyboard-oriented settings window."""

from __future__ import annotations

from pathlib import Path


SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


def test_settings_have_real_native_pages_and_explicit_accessible_names() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "self.notebook = wx.Notebook(self)" in source
    assert 'i18n.tr("settings.categories.accessibleName")' in source
    for key in (
        "general", "radio", "podcasts", "keyboard", "windows", "updates",
        "accessibility",
    ):
        assert f'i18n.tr("settings.tab.{key}")' in source
    assert 'self.interface_language.SetName(' in source
    assert 'i18n.tr("settings.interfaceLanguage.accessibleName")' in source
    assert 'self.startup_target.SetName("Wyświetlaj po uruchomieniu")' in source
    assert 'choices=["Ostatnia używana sesja", "Menedżer sesji"]' in source
    assert 'self.silence.SetName("Dodatkowa cisza po naturalnym końcu utworu")' in source
    assert 'self.tempo.SetName("Sposób przeliczania zmienionego tempa")' in source
    assert 'self.keep_audio_edit_backups.SetName("Zachowuj kopie po edycji")' in source
    assert '"Długość przeskoku pod Ctrl+Alt+strzałką w lewo i w prawo"' in source
    assert 'self.global_transient_previews.SetName(' in source
    assert '"Wspólne podglądy nowych odcinków i transmisji we wszystkich sesjach"' in source
    assert 'self.remember_local_playback_positions.SetName(' in source
    assert '"Pamiętaj pozycję odtwarzania lokalnych plików"' in source
    assert 'self.detailed_hints.SetName(' in source
    assert '"Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach"' in source
    assert 'self.message_enabled.SetName("Włącz komunikaty dostępności")' in source
    assert 'self.message_seek_messages.SetName(' in source
    assert '"Włącz automatyczne komunikaty odtwarzacza"' in source
    assert 'self.message_arrow_seek_messages.SetName(' in source
    assert '"Oznajmiaj przewijanie strzałkami oraz klawiszami Home i End"' in source
    assert 'self.message_percentage_seek_messages.SetName(' in source
    assert '"Oznajmiaj skoki klawiszami cyfr"' in source
    assert 'self.message_volume_messages.SetName("Oznajmiaj zmiany głośności")' in source
    assert 'self.message_playback_messages.SetName(' in source
    assert '"Oznajmiaj odtwarzanie i pauzę"' in source
    assert 'self.message_bookmark_navigation_messages.SetName(' in source
    assert '"Oznajmiaj nawigację po zakładkach"' in source
    assert 'self.message_percent_announcement.SetName(' in source
    assert 'choices=["Tylko procent", "Tylko czas", "Procent i czas"]' in source
    assert 'i18n.tr("settings.messageTemplates.event.accessibleName")' in source
    assert 'self.message_template_text.SetName(' in source
    assert 'i18n.tr("settings.messageTemplates.text.accessibleName")' in source
    assert "template_labels(self._message_template_language)[key]" in source


def test_settings_pages_and_storage_controls_follow_the_user_workflow() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    page_calls = [
        'general, i18n.tr("settings.tab.general"), select=True',
        'self.notebook.AddPage(radio, i18n.tr("settings.tab.radio"))',
        'self.notebook.AddPage(podcasts, i18n.tr("settings.tab.podcasts"))',
        'messages, i18n.tr("settings.tab.accessibility")',
        'self.notebook.AddPage(keyboard, i18n.tr("settings.tab.keyboard"))',
        'self.notebook.AddPage(updates, i18n.tr("settings.tab.updates"))',
        'self.notebook.AddPage(integration, i18n.tr("settings.tab.windows"))',
    ]
    assert [source.index(call) for call in page_calls] == sorted(
        source.index(call) for call in page_calls
    )

    dialog = source[source.index("class GeneralPlaybackOptionsDialog") :]
    dialog = dialog[: dialog.index("class LiteFrame")]
    radio_order = [
        "self.recording_folder_mode = wx.Choice(",
        "self.recording_folder = FocusableReadOnlyTextCtrl(",
        "self.recording_format = wx.Choice(",
        "self.timeshift = wx.Choice(",
        "self.recognition_monitoring = wx.CheckBox(",
        "self.prefer_station_folder = wx.CheckBox(",
    ]
    assert [dialog.index(item) for item in radio_order] == sorted(
        dialog.index(item) for item in radio_order
    )
    podcast_order = [
        "self.podcast_folder = FocusableReadOnlyTextCtrl(",
        "self.youtube_video_format = wx.Choice(",
        "self.podcast_rss_interval = wx.SpinCtrl(",
        "self.podcast_youtube_interval = wx.SpinCtrl(",
        "self.podcast_refresh_batch = wx.SpinCtrl(",
    ]
    assert [dialog.index(item) for item in podcast_order] == sorted(
        dialog.index(item) for item in podcast_order
    )
    assert 'recording_heading.SetName("Sekcja zapisywanie nagrań radia")' in dialog
    assert 'podcast_storage_heading.SetName(' in dialog
    assert 'podcast_refresh_heading.SetName(' in dialog
    assert dialog.index("self.recording_folder = FocusableReadOnlyTextCtrl(") < dialog.index(
        "self.radio_separate_station_folders = wx.CheckBox("
    ) < dialog.index("self.recording_format = wx.Choice(")
    assert dialog.index("self.podcast_folder = FocusableReadOnlyTextCtrl(") < dialog.index(
        "self.podcast_separate_source_folders = wx.CheckBox("
    ) < dialog.index("self.youtube_video_format = wx.Choice(")
    assert 'self.radio_separate_station_folders.SetName(' in dialog
    assert 'self.podcast_separate_source_folders.SetName(' in dialog


def test_message_options_precede_the_inline_template_editor() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dialog = source[source.index("class GeneralPlaybackOptionsDialog") :]
    dialog = dialog[: dialog.index("class LiteFrame")]
    expected_order = [
        "self.detailed_hints = wx.CheckBox(",
        "self.recognition_announcements = wx.CheckBox(",
        "activity_heading = wx.StaticText(",
        "self.playback_position = wx.Choice(",
        "self.recording_sound = wx.CheckBox(",
        "templates_heading = wx.StaticText(",
        "self.message_templates = wx.ListBox(",
        "self.message_template_text = wx.TextCtrl(",
    ]
    assert [dialog.index(item) for item in expected_order] == sorted(
        dialog.index(item) for item in expected_order
    )
    assert 'i18n.tr("settings.messageTemplates.heading.accessibleName")' in dialog
    assert "self.message_templates.SetHelpText(" in dialog
    assert "self.message_template_text.SetHelpText(" in dialog


def test_enter_save_has_one_short_confirmation_and_reconfigures_the_host() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert 'save.SetName(i18n.tr("settings.save.accessibleName"))' in source
    assert 'cancel.SetLabel(i18n.tr("settings.cancel.label"))' in source
    assert 'cancel.SetName(i18n.tr("settings.cancel.accessibleName"))' in source
    assert 'save.SetDefault()' in source
    assert 'self.announcer.say("Zapisano ustawienia")' in source
    assert 'client.configure_audio(' in source
    method = source[source.index("def _show_general_playback_options") :]
    method = method[:method.index("def _with_radio_recording_options")]
    assert "dialog.Destroy()\n        self._restore_focus_after_dialog()" in method
    assert method.index('self.announcer.say("Zapisano ustawienia")') < method.index(
        "client = self.client"
    )
    assert method.count('self.announcer.say("Zapisano ustawienia")') == 1
    assert method.index("if not self._save_state():") < method.index(
        "i18n.reconfigure_window_tree("
    )
    assert method.index("i18n.reconfigure_window_tree(") < method.index(
        "templates=_message_templates_for_language("
    )
    assert method.index("i18n.reconfigure_window_tree(") < method.index(
        "self._build_menu()"
    )
    assert method.index("i18n.reconfigure_window_tree(") < method.index(
        "media_list.relocalize_interface()"
    )


def test_all_exposed_choices_use_intentional_user_facing_labels() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "choices=[TEMPO_LABELS[value] for value in self._tempo_values]" in source
    assert "session_options.inter_track_silence_label(value)" in source
    assert "STATE_POSITION_LABELS" in source
    assert 'self.timeshift.SetName("Długość bufora transmisji")' in source
    assert '"keep_audio_edit_backups": self.keep_audio_edit_backups.GetValue()' in source
    assert '"startup_target": self._startup_target_values[' in source
    assert '"custom_seek_seconds": self._custom_seek_seconds' in source
    assert '"global_transient_previews": self.global_transient_previews.GetValue()' in source
    assert '"remember_local_playback_positions": (' in source
    assert '"detailed_hints": self.detailed_hints.GetValue()' in source
    assert '"message_enabled": self.message_enabled.GetValue()' in source
    assert '"message_seek_messages": self.message_seek_messages.GetValue()' in source
    assert '"message_arrow_seek_messages": (' in source
    assert '"message_percentage_seek_messages": (' in source
    assert '"message_volume_messages": self.message_volume_messages.GetValue()' in source
    assert '"message_playback_messages": (' in source
    assert '"message_bookmark_navigation_messages": (' in source
    assert '"message_percent_announcement": self._message_percent_values[' in source
    assert '"message_templates_by_language": {' in source
    assert '"interface_language": self._interface_language_values[' in source
    assert "self.recording_folder = FocusableReadOnlyTextCtrl(" in source
    assert 'self.recording_folder.SetName("Domyślny folder nagrywania radia")' in source
    assert 'self.recording_folder_mode.SetName("Położenie nagrań Radia")' in source
    assert '"Ten sam folder co pobieranie Podcastów"' in source
    assert 'self.recording_format.SetName("Format nagrania radia")' in source
    assert 'self.recording_bitrate.SetName("Bitrate nagrania MP3 lub AAC")' in source
    assert "self.podcast_folder = FocusableReadOnlyTextCtrl(" in source
    assert 'self.podcast_folder.SetName(' in source
    assert (
        '"Domyślny folder zapisu pobranych podcastów i dźwięku YouTube"'
        in source
    )
    assert (
        '"Wybierz domyślny folder zapisu podcastów i dźwięku YouTube"'
        in source
    )
    assert (
        '"Użyj domyślnego folderu zapisu podcastów i dźwięku YouTube w AMC"'
        in source
    )
    assert 'self.podcast_rss_interval.SetName(' in source
    assert 'self.podcast_youtube_interval.SetName(' in source
    assert 'self.podcast_refresh_batch.SetName(' in source


def test_tempo_choices_describe_results_without_speaking_library_names() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    labels = source[source.index("TEMPO_LABELS = {") : source.index("}\n", source.index("TEMPO_LABELS = {") ) + 2]
    assert "Najlepsza czytelność mowy" in labels
    assert "Najlepsza jakość muzyki" in labels
    assert "Tryb zgodności" in labels
    assert "Speedy" not in labels
    assert "Signalsmith" not in labels
    assert "SoundTouch" not in labels


def test_windows_integration_controls_have_explicit_names_help_and_real_effect() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert 'self.shell_status.SetName(' in source
    assert 'self.shell_context_menu.SetName(' in source
    assert 'self.shell_open_with.SetName(' in source
    assert 'groups_heading.SetName(' in source
    assert 'control.SetName(group_name)' in source
    assert 'control.SetHelpText(" ".join(help_parts))' in source
    assert 'self.open_default_apps.SetName(' in source
    assert '"shell_integration": (' in source
    assert "engine_for_settings.shell_integration_status()" in source
    assert "engine_for_settings.general_settings_defaults()" in source
    assert "or self._default_recording_folder" in source
    assert "or self._default_podcast_folder" in source
    assert "client.apply_shell_integration(" in source
    assert "engine.open_windows_default_apps()" in source


def test_update_page_uses_focusable_status_fields_and_the_shared_command() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "self.update_channel = FocusableReadOnlyTextCtrl(" in source
    assert 'self.update_channel.SetName("Kanał aktualizacji programu")' in source
    assert "self.component_ffmpeg_status = FocusableReadOnlyTextCtrl(" in source
    assert 'self.component_ffmpeg_status.SetName("Stan składnika FFmpeg")' in source
    assert "self.component_yt_dlp_status = FocusableReadOnlyTextCtrl(" in source
    assert 'self.component_yt_dlp_status.SetName("Stan składnika yt-dlp")' in source
    assert 'self.component_check_automatically.SetName(' in source
    assert 'self.component_download_automatically.SetName(' in source
    assert 'self.application_check_automatically.SetName(' in source
    assert 'self.check_components_now.SetName(' in source
    assert "self._check_media_components(completed)" in source
    assert "engine_for_settings.component_update_status()" in source
    assert "return self._update_media_components(on_complete=on_complete)" in source
    assert '"component_check_automatically": (' in source
    assert '"component_download_automatically": (' in source
    assert '"application_check_automatically": (' in source


def test_podcast_settings_reach_the_database_owner_and_automatic_refresh() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert '"Liczba źródeł sprawdzanych w &jednym przebiegu:"' in source
    assert '"Liczba źródeł sprawdzanych &jednocześnie:"' not in source
    assert "client.configure_podcast_global_settings(" in source
    assert "client.refresh_podcasts_automatically" in source
    assert 'self.runner.submit(\n            "podcast-auto-refresh"' in source


def test_radio_settings_reach_recording_and_schedule_payloads() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert "preferences_with_overrides(" in source
    assert 'chosen.pop("radio_schedule_wake_override")' in source
    assert "client.sync_radio_schedules(" in source
    assert "options.timeshift_minutes" in source


def test_audio_edit_backup_setting_reaches_both_destructive_edit_paths() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert source.count("resolve_keep_audio_edit_backups(") == 3
    assert source.count("self.options.keep_audio_edit_backups") == 3


def test_custom_seek_setting_is_applied_on_start_and_immediately_after_save() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert source.count("self.messages.custom_seek_seconds = resolve_custom_seek_seconds(") == 2
    assert "parse_custom_seek_length(self.custom_seek.GetValue())" in source
    assert '"Nie zapisano ustawień"' in source


def test_global_preview_setting_gates_shortcuts_before_session_specific_code() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dispatch = source[source.index("    def _dispatch(self, action: Action)") :]
    dispatch = dispatch[:dispatch.index("    def ", 5)]
    gate = dispatch.index("action in TRANSIENT_PREVIEW_ACTIONS")
    tidal = dispatch.index("self.navigator.active is SessionId.TIDAL")
    assert gate < tidal
    assert source.count("self._global_transient_previews = resolve_global_transient_previews(") == 2


def test_local_resume_setting_reaches_start_save_and_background_checkpoint() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert source.count("self._remember_local_playback_positions = (") == 2
    assert source.count("session_options.configure_resume_preferences(") == 7
    assert "self._checkpoint_local_if_due()" in source
    assert '"local-checkpoint"' in source


def test_detailed_hints_setting_updates_filter_and_search_accessible_help() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert source.count("self._detailed_hints = resolve_detailed_hints(") == 2
    assert "self._apply_detailed_hints()" in source
    assert "detailed_hints=self._detailed_hints" in source
    assert "self.query.SetHelpText(" in source
    assert "self.results.SetHelpText(" in source
    assert "self._help_provider = wx.SimpleHelpProvider()" in source
    assert "wx.HelpProvider.Set(self._help_provider)" in source


def test_message_settings_override_the_inherited_policy_and_toggle_persists() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    assert source.count("apply_message_overrides(") == 2
    assert source.count("enabled=self.options.message_enabled") == 2
    assert source.count("templates=_message_templates_for_language(") == 2
    assert "self.options.message_seek_messages = self.messages.seek_messages" in source
    assert "if not self._save_state():" in source
    assert "self.messages.announces_playback" in source
    assert "self.announcer.show(message)" in source


def test_session_list_startup_target_opens_only_after_the_host_is_ready() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    startup = source[source.index("    def _start_engine(self)") :]
    startup = startup[:startup.index("    def _on_engine_event")]
    assert 'if self.options.startup_target == "SessionList":' in startup
    assert "wx.CallAfter(self._show_session_list)" in startup
    assert startup.index("self._load_initial_content()") < startup.index(
        "wx.CallAfter(self._show_session_list)"
    )
