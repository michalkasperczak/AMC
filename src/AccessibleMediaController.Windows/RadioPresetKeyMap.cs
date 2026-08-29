using System.Windows.Input;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows;

internal static class RadioPresetKeyMap
{
    private const int VirtualKey0 = 0x30;
    private const int VirtualKey1 = 0x31;
    private const int VirtualKey9 = 0x39;
    private const int VirtualKeyNumpad0 = 0x60;
    private const int VirtualKeyNumpad1 = 0x61;
    private const int VirtualKeyNumpad9 = 0x69;
    private const int VirtualKeySubtract = 0x6D;
    private const int VirtualKeyAdd = 0x6B;
    private const int VirtualKeyOemMinus = 0xBD;
    private const int VirtualKeyOemPlus = 0xBB;

    public static bool TryGetSlot(Key key, out int slot)
    {
        if (key is >= Key.D1 and <= Key.D9)
        {
            slot = (int)key - (int)Key.D0;
            return true;
        }
        if (key is >= Key.NumPad1 and <= Key.NumPad9)
        {
            slot = (int)key - (int)Key.NumPad0;
            return true;
        }
        slot = key switch
        {
            Key.D0 or Key.NumPad0 => 10,
            Key.OemMinus or Key.Subtract => 11,
            Key.OemPlus or Key.Add => 12,
            _ => 0
        };
        return slot != 0;
    }

    public static bool TryGetSlotFromVirtualKey(int virtualKey, out int slot)
    {
        if (virtualKey is >= VirtualKey1 and <= VirtualKey9)
        {
            slot = virtualKey - VirtualKey0;
            return true;
        }
        if (virtualKey is >= VirtualKeyNumpad1 and <= VirtualKeyNumpad9)
        {
            slot = virtualKey - VirtualKeyNumpad0;
            return true;
        }
        slot = virtualKey switch
        {
            VirtualKey0 or VirtualKeyNumpad0 => 10,
            VirtualKeyOemMinus or VirtualKeySubtract => 11,
            VirtualKeyOemPlus or VirtualKeyAdd => 12,
            _ => 0
        };
        return slot != 0;
    }

    public static string DirectShortcutLabel(int slot) =>
        RadioPresetSlots.SpokenShortcutLabel(slot);
}
