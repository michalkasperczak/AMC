"""Dostepne okno wyniku sprawdzania aktualizacji calego AMC."""

from __future__ import annotations

import wx

from .application_update import ApplicationUpdateCheck


class ApplicationUpdateDialog(wx.Dialog):
    """Pole tylko do odczytu z nowosciami i jednoznaczne dzialania."""

    def __init__(self, parent: wx.Window, check: ApplicationUpdateCheck) -> None:
        super().__init__(
            parent,
            title=f"Aktualizacja {check.profile.display_name}",
            style=wx.DEFAULT_DIALOG_STYLE | wx.RESIZE_BORDER | wx.STAY_ON_TOP,
        )
        panel = wx.Panel(self)
        outer = wx.BoxSizer(wx.VERTICAL)

        label = wx.StaticText(panel, label="&Informacje o aktualizacji:")
        outer.Add(label, 0, wx.LEFT | wx.RIGHT | wx.TOP, 12)

        self.information = wx.TextCtrl(
            panel,
            value=check.information_text,
            style=wx.TE_MULTILINE | wx.TE_READONLY | wx.TE_RICH2,
            size=(560, 260),
            name="Informacje o aktualizacji AMC",
        )
        self.information.SetName("Informacje o aktualizacji AMC")
        outer.Add(self.information, 1, wx.EXPAND | wx.ALL, 12)

        buttons = wx.StdDialogButtonSizer()
        self.update_button: wx.Button | None = None
        if check.available:
            self.update_button = wx.Button(panel, wx.ID_OK, "&Aktualizuj")
            self.update_button.SetName("Aktualizuj AMC")
            buttons.AddButton(self.update_button)
            self.SetAffirmativeId(wx.ID_OK)
        close_button = wx.Button(panel, wx.ID_CANCEL, "&Zamknij")
        close_button.SetName("Zamknij okno aktualizacji")
        buttons.AddButton(close_button)
        buttons.Realize()
        outer.Add(buttons, 0, wx.ALIGN_RIGHT | wx.LEFT | wx.RIGHT | wx.BOTTOM, 12)

        panel.SetSizer(outer)
        dialog_sizer = wx.BoxSizer(wx.VERTICAL)
        dialog_sizer.Add(panel, 1, wx.EXPAND)
        self.SetSizerAndFit(dialog_sizer)
        self.SetMinSize((520, 300))
        self.CentreOnParent()

        self.Bind(wx.EVT_SHOW, self._on_show)

    def _on_show(self, event: wx.ShowEvent) -> None:
        event.Skip()
        if not event.IsShown():
            return
        self.Raise()
        self.information.SetInsertionPoint(0)
        self.information.SetFocus()
