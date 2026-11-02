"""Keyboard-accessible selection of a second Google Cast audio target."""

import wx

from . import i18n


class MirrorDeviceDialog(wx.Dialog):
    def __init__(self, parent, session_name, devices, selected_id=None):
        super().__init__(parent, title=i18n.tr("mirror.title", session=session_name))
        self._ids = [None] + [device.device_id for device in devices]
        panel = wx.Panel(self)
        label = wx.StaticText(panel, label=i18n.tr("mirror.devices"))
        self.devices = wx.ListBox(
            panel,
            choices=[i18n.tr("mirror.off")] + [device.label for device in devices],
        )
        self.devices.SetName(i18n.tr("mirror.devices"))
        self.devices.SetSelection(self._ids.index(selected_id) if selected_id in self._ids else 0)
        hint = wx.StaticText(panel, label=i18n.tr("mirror.hint"))
        hint.Wrap(540)
        refresh = wx.Button(panel, wx.ID_REFRESH, i18n.tr("cast.refresh"))
        refresh.SetName(i18n.tr("cast.refresh"))
        inner = wx.BoxSizer(wx.VERTICAL)
        inner.Add(label, 0, wx.BOTTOM, 6)
        inner.Add(self.devices, 1, wx.EXPAND | wx.BOTTOM, 10)
        inner.Add(hint, 0, wx.EXPAND | wx.BOTTOM, 10)
        inner.Add(refresh)
        panel.SetSizer(inner)
        buttons = self.CreateStdDialogButtonSizer(wx.OK | wx.CANCEL)
        self.FindWindowById(wx.ID_OK).SetLabel(i18n.tr("mirror.choose"))
        self.FindWindowById(wx.ID_OK).SetName(i18n.tr("mirror.choose"))
        outer = wx.BoxSizer(wx.VERTICAL)
        outer.Add(panel, 1, wx.ALL | wx.EXPAND, 12)
        outer.Add(buttons, 0, wx.ALL | wx.ALIGN_RIGHT, 12)
        self.SetSizerAndFit(outer)
        self.SetMinSize((570, 320))
        refresh.Bind(wx.EVT_BUTTON, lambda event: self.EndModal(wx.ID_REFRESH))
        self.devices.Bind(wx.EVT_LISTBOX_DCLICK, lambda event: self.EndModal(wx.ID_OK))
        self.devices.SetFocus()

    @property
    def selected_device_id(self):
        selection = self.devices.GetSelection()
        return self._ids[selection] if 0 <= selection < len(self._ids) else None
