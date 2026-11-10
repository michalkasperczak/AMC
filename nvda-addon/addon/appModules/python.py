"""Let NVDA find the AMC wxPython status bar without changing NVDA gestures.

NVDA's generic status-bar lookup probes the lower-left window pixel. On the
AMC frame this can miss the native wx status bar and fall back to list text.
The GUI is hosted by python.exe, so this app module leaves every other Python
window on NVDA's normal lookup path.
"""

import api
import appModuleHandler
import controlTypes


_AMC_TITLES = ("AMC Test — ", "AMC Public — ")


class AppModule(appModuleHandler.AppModule):
    def _get_statusBar(self):
        foreground = api.getForegroundObject()
        title = str(getattr(foreground, "name", "") or "")
        if not title.startswith(_AMC_TITLES):
            raise NotImplementedError

        # The wx.Frame exposes its native status bar as a direct child.
        # Do not descend into the potentially huge media list.
        for child in getattr(foreground, "children", ()) or ():
            if child.role == controlTypes.Role.STATUSBAR:
                return child

        # Preserve NVDA's normal position-based lookup if the UI changes.
        raise NotImplementedError
