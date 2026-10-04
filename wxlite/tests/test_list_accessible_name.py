"""Nazwa i rola SAMEJ listy -- to, czego ``SysListView32`` nie podaje.

CO TE TESTY PILNUJA. Objaw zgloszony przez uzytkownika: na widoku zakladek bez
wpisow czytnik mowil "nieznane" przed naszym naglowkiem "Biblioteka — Zakladki,
pusto". Sonda MSAA w produkcyjnym runtime (kwit
``list-speech-after422/zdarzenia15.jsonl``) zmierzyla przyczyne:

    pusta lista  BEZ nakladki -> hr=S_FALSE (1), accName=None
    pusta lista  Z  nakladka  -> hr=S_OK   (0), accName='Biblioteka — Zakladki, pusto'
    pelna lista  BEZ nakladki -> hr=S_FALSE (1), accName=None
    pelna lista  Z  nakladka  -> hr=S_OK   (0), accName='Biblioteka — Zakladki, pusto'

Czyli sama kontrolka NIGDY nie miala nazwy MSAA. Dopoki stal na niej wiersz,
czytnik mowil wiersz i nie bylo tego slychac; na pustym zbiorze nie ma wiersza,
wiec zostaje obiekt bez nazwy -- i to jest "nieznane".

Te testy sprawdzaja KONTRAKT nakladki (co zwraca dla ktorego ``childId``), a nie
sama mowe. Mowy test jednostkowy nie dowodzi -- dowodem jest kwit z zywego
czytnika.
"""
from __future__ import annotations

import unittest

import wx

from amc_wx_lite.gui import MediaListAccessible


class FakeWindow:
    """Minimalne okno: nakladka pyta je tylko o nazwe."""

    def __init__(self, name: str) -> None:
        self._name = name

    def GetName(self) -> str:  # noqa: N802 - API wx
        return self._name


class ListNameIsExposedToTheScreenReader(unittest.TestCase):
    def test_empty_list_has_a_name_instead_of_unknown(self) -> None:
        """Pusta lista musi podac SWOJA nazwe -- nie ma wiersza, ktory ja zastapi."""
        overlay = MediaListAccessible(FakeWindow("Biblioteka — Zakładki, pusto"))

        status, name = overlay.GetName(0)

        self.assertEqual(status, wx.ACC_OK)
        self.assertEqual(name, "Biblioteka — Zakładki, pusto")

    def test_the_list_reports_the_list_role(self) -> None:
        overlay = MediaListAccessible(FakeWindow("Biblioteka — Wszystkie pliki"))

        status, role = overlay.GetRole(0)

        self.assertEqual(status, wx.ACC_OK)
        self.assertEqual(role, wx.ROLE_SYSTEM_LIST)

    def test_rows_keep_their_native_names(self) -> None:
        """Wiersze zostaja natywne: inaczej zwykla strzalka przestalaby je czytac.

        ``ACC_NOT_IMPLEMENTED`` znaczy "odpowiedz tak jak dotychczas", wiec
        nazwy wierszy, kolumny i pozycja "N z M" pochodza nadal od kontrolki.
        """
        overlay = MediaListAccessible(FakeWindow("Biblioteka — Wszystkie pliki"))

        for child_id in (1, 2, 2309):
            with self.subTest(child_id=child_id):
                self.assertEqual(overlay.GetName(child_id)[0], wx.ACC_NOT_IMPLEMENTED)
                self.assertEqual(overlay.GetRole(child_id)[0], wx.ACC_NOT_IMPLEMENTED)

    def test_a_window_without_a_name_does_not_invent_one(self) -> None:
        """Brak nazwy okna oddajemy kontrolce, zamiast podawac pusty napis."""
        overlay = MediaListAccessible(FakeWindow(""))

        self.assertEqual(overlay.GetName(0)[0], wx.ACC_NOT_IMPLEMENTED)


if __name__ == "__main__":
    unittest.main()
