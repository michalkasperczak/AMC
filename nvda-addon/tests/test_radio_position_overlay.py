"""Nakladka ma zmienic NATYWNE ``positionInfo`` NVDA, i tylko w liscie AMC.

ZRODLO KONTRAKTU (odczytane, nie zalozone)
------------------------------------------
* ``NVDAObjects/__init__.py:125-131`` -- NVDA wola
  ``chooseNVDAObjectOverlayClasses`` KAZDEGO uruchomionego globalPluginu.
  Dlatego appModule NIE jest potrzebny i nadal go nie dodajemy (pilnuje tego
  ``test_gesture_scope``).
* ``NVDAObjects/IAccessible/sysListView32.py:463-466`` -- ``ListItem``
  liczy ``similarItemsInGroup`` z ``LVM_GETITEMCOUNT``. To jest ten licznik.
* ``NVDAObjects/__init__.py:1108-1112`` -- bazowe ``_get_positionInfo``
  zwraca ``{}``, czyli pusty slownik jest LEGALNA odpowiedzia w tym API, a
  nie bledem.
* ``speech/speech.py:756-762`` -- mowa bierze klucze ``indexInGroup`` i
  ``similarItemsInGroup`` tylko gdy sa OBECNE w slowniku.

GRANICA TYCH TESTOW
-------------------
Nie ma tu NVDA, Windows ani czytnika. Atrapy odwzorowuja KSZTALT API
odczytany wyzej. Dowodza decyzji nakladki (kiedy ``{}``, kiedy ``super``),
nie dowodza, ze zywy NVDA zamilknie. To moze pokazac wylacznie odbior.
"""

import importlib.util
from pathlib import Path
import sys
import types
import unittest

ROOT = Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "nvda-addon/addon/globalPlugins/amcController"


def load(name):
    """Wczytaj modul wtyczki BEZ importowania NVDA."""
    spec = importlib.util.spec_from_file_location("amc_" + name, PLUGIN / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class FakeUser32:
    """``GetPropW`` z pamieci. Kazdy odczyt jest liczony."""

    def __init__(self, props=None):
        self.props = dict(props or {})
        self.reads = []

    def get_prop(self, hwnd, name):
        self.reads.append((hwnd, name))
        return self.props.get((hwnd, name), 0)


NATIVE = {"indexInGroup": 3, "similarItemsInGroup": 37}


def make_item(overlay, hwnd=4242, native=None):
    """Obiekt listy z natywnym ``_get_positionInfo`` pod nakladka.

    Kolejnosc baz odwzorowuje NVDA: nakladka jest PRZED klasa natywna, wiec
    ``super()`` w nakladce trafia w natywna implementacje.

    Metaklasa odwzorowuje ``baseObject.AutoPropertyType`` (NVDA,
    ``source/baseObject.py:60-119``): to ona zamienia ``_get_positionInfo`` w
    czytana wlasciwosc ``positionInfo``, wiec bez niej test sprawdzalby
    sciezke, ktorej NVDA nie uzywa. GRANICA: to odwzorowanie KSZTALTU API,
    nie oryginalna metaklasa -- nie ma tu cache'u ani ``invalidateCache``.
    """

    class AutoProperty(type):
        def __new__(mcls, name, bases, namespace):
            cls = super().__new__(mcls, name, bases, namespace)
            cls.positionInfo = property(lambda self: self._get_positionInfo())
            return cls

    class Native:
        def __init__(self):
            self.windowHandle = hwnd

        def _get_positionInfo(self):
            return dict(NATIVE if native is None else native)

    class Item(overlay, Native, metaclass=AutoProperty):
        pass

    return Item()


class OverlayDecisionTests(unittest.TestCase):
    def setUp(self):
        self.module = load("radioList")

    # ---------------------------------------------------------- ukrywanie

    def test_pusty_slownik_gdy_oba_znaczniki_sa_w_trybie_ukrywania(self):
        """To jest cala funkcja: licznik nie dociera do mowy."""
        fake = FakeUser32({
            (4242, self.module.PRESENCE_PROP): 1,
            (4242, self.module.MODE_PROP): 1,
        })
        self.module.setPropReader(fake.get_prop)
        item = make_item(self.module.AmcRadioListItem)

        self.assertEqual(item.positionInfo, {})

    def test_brak_znacznika_TRYBU_oddaje_NATYWNY_licznik_bez_zmian(self):
        """Normalny licznik po wlaczeniu opcji -- dokladnie natywne wartosci."""
        fake = FakeUser32({(4242, self.module.PRESENCE_PROP): 1})
        self.module.setPropReader(fake.get_prop)
        item = make_item(self.module.AmcRadioListItem)

        self.assertEqual(item.positionInfo, NATIVE)

    def test_obce_okno_bez_znacznikow_jest_NIETKNIETE(self):
        """Pliki i inne aplikacje: nakladka musi oddac natywne super()."""
        fake = FakeUser32()
        self.module.setPropReader(fake.get_prop)
        item = make_item(self.module.AmcRadioListItem, hwnd=999)

        self.assertEqual(item.positionInfo, NATIVE)

    def test_sam_znacznik_TRYBU_bez_OBECNOSCI_nie_wystarcza(self):
        """Dokladny marker, nie jeden z dwoch -- inaczej obce okno moglo by trafic."""
        fake = FakeUser32({(4242, self.module.MODE_PROP): 1})
        self.module.setPropReader(fake.get_prop)
        item = make_item(self.module.AmcRadioListItem)

        self.assertEqual(item.positionInfo, NATIVE)

    def test_odczyt_jest_ZA_KAZDYM_RAZEM_bo_opcja_zmienia_sie_bez_refocus(self):
        """Nakladke NVDA dobiera RAZ, przy tworzeniu obiektu.

        Gdyby nakladka zapamietala tryb w ``initOverlayClass``, przelaczenie
        opcji dzialaloby tylko po ponownym wejsciu na liste. Dlatego tryb
        czytamy przy KAZDYM odczycie pozycji.
        """
        fake = FakeUser32({
            (4242, self.module.PRESENCE_PROP): 1,
            (4242, self.module.MODE_PROP): 1,
        })
        self.module.setPropReader(fake.get_prop)
        item = make_item(self.module.AmcRadioListItem)

        self.assertEqual(item.positionInfo, {})
        del fake.props[(4242, self.module.MODE_PROP)]
        # BEZ nowego obiektu i bez zmiany fokusu.
        self.assertEqual(item.positionInfo, NATIVE)
        self.assertIn((4242, self.module.MODE_PROP), fake.reads)

    def test_blad_odczytu_znacznika_ZOSTAWIA_natywne_zachowanie(self):
        """Awaria nie moze uciszyc licznika w nieznanym miejscu."""

        def boom(*_args):
            raise OSError("user32")

        self.module.setPropReader(boom)
        item = make_item(self.module.AmcRadioListItem)

        self.assertEqual(item.positionInfo, NATIVE)

    def test_nakladka_nie_rusza_niczego_poza_pozycja(self):
        """Zaden przechwyt mowy, zadne gesty, zadna nazwa ani rola."""
        allowed = {"_get_positionInfo", "__module__", "__qualname__", "__doc__", "__dict__", "__weakref__"}
        extra = set(vars(self.module.AmcRadioListItem)) - allowed
        self.assertFalse(extra, f"nakladka rusza wiecej niz pozycje: {extra}")


class OverlaySelectionTests(unittest.TestCase):
    """Wybor nakladki: WYLACZNIE wiersz oznaczonej listy AMC."""

    def setUp(self):
        self.module = load("radioList")

    def make_obj(self, hwnd=4242, role="listItem", child_id=3):
        obj = types.SimpleNamespace()
        obj.windowHandle = hwnd
        obj.role = role
        obj.IAccessibleChildID = child_id
        obj.windowClassName = "SysListView32"
        return obj

    def test_oznaczony_wiersz_dostaje_nakladke(self):
        fake = FakeUser32({(4242, self.module.PRESENCE_PROP): 1})
        self.module.setPropReader(fake.get_prop)
        clsList = []
        self.module.chooseOverlay(self.make_obj(), clsList)
        self.assertEqual(clsList, [self.module.AmcRadioListItem])

    def test_nieoznaczone_okno_nie_dostaje_nic(self):
        self.module.setPropReader(FakeUser32().get_prop)
        clsList = []
        self.module.chooseOverlay(self.make_obj(hwnd=999), clsList)
        self.assertEqual(clsList, [])

    def test_sama_kontrolka_listy_nie_jest_wierszem(self):
        """``positionInfo`` wiersza, nie calej listy -- zapewnienie waskie."""
        fake = FakeUser32({(4242, self.module.PRESENCE_PROP): 1})
        self.module.setPropReader(fake.get_prop)
        clsList = []
        self.module.chooseOverlay(self.make_obj(role="list", child_id=0), clsList)
        self.assertEqual(clsList, [])

    def test_obca_klasa_okna_jest_odrzucana(self):
        fake = FakeUser32({(4242, self.module.PRESENCE_PROP): 1})
        self.module.setPropReader(fake.get_prop)
        clsList = []
        obj = self.make_obj()
        obj.windowClassName = "ObcaKlasa"
        self.module.chooseOverlay(obj, clsList)
        self.assertEqual(clsList, [])

    def test_obiekt_bez_wymaganych_pol_nie_wywraca_NVDA(self):
        self.module.setPropReader(FakeUser32().get_prop)
        clsList = []
        self.module.chooseOverlay(types.SimpleNamespace(), clsList)
        self.assertEqual(clsList, [])

    def test_blad_odczytu_nie_dolacza_nakladki_i_nie_leci_wyjatkiem(self):
        def boom(*_args):
            raise OSError("user32")

        self.module.setPropReader(boom)
        clsList = []
        self.module.chooseOverlay(self.make_obj(), clsList)
        self.assertEqual(clsList, [])


class PluginWiringTests(unittest.TestCase):
    """Plugin musi NAPRAWDE oddac nakladke NVDA, nie tylko ja zdefiniowac."""

    def test_plugin_definiuje_chooseNVDAObjectOverlayClasses_we_WLASNEJ_klasie(self):
        """NVDAObjects/__init__.py:127 sprawdza ``plugin.__class__.__dict__``.

        Metoda odziedziczona albo przypisana na instancji NIE zostanie
        wywolana -- to ten warunek, ktory decyduje o calej funkcji.
        """
        import ast

        tree = ast.parse((PLUGIN / "__init__.py").read_text(encoding="utf-8"))
        plugin = next(
            node for node in tree.body
            if isinstance(node, ast.ClassDef) and node.name == "GlobalPlugin"
        )
        methods = {n.name for n in plugin.body if isinstance(n, ast.FunctionDef)}
        self.assertIn("chooseNVDAObjectOverlayClasses", methods)

    def test_nadal_bez_appModule(self):
        """Teza o niemoznosci opierala sie na appModule. Zakaz ZOSTAJE."""
        self.assertFalse((ROOT / "nvda-addon/addon/appModules").exists())

    def test_nakladka_nie_dotyka_globalnej_konfiguracji_ani_mowy(self):
        """Sprawdzamy KOD, nie prozę.

        Pierwsza wersja tego testu szukala napisow w calym pliku i trafila we
        WLASNY komentarz ("zadnej zmiany config.conf"), czyli w zdanie, ktore
        mowi dokladnie to, czego test pilnuje. Napis w komentarzu nic nie
        wykonuje -- mierzalnym zakazem sa importy i wywolania. Dlatego
        patrzymy na drzewo skladniowe: nazwy importowanych modulow i nazwy
        wolanych atrybutow.
        """
        import ast

        tree = ast.parse((PLUGIN / "radioList.py").read_text(encoding="utf-8"))
        imported = set()
        attributes = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imported.update(alias.name for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imported.add(node.module)
            elif isinstance(node, ast.Attribute):
                attributes.add(node.attr)

        for forbidden in ("config", "speech", "ui", "inputCore", "api", "globalVars"):
            self.assertNotIn(forbidden, imported, f"nakladka importuje {forbidden}")
        for forbidden in ("speak", "setSpeechMode", "message", "conf"):
            self.assertNotIn(forbidden, attributes, f"nakladka wola .{forbidden}")
        # Kontrdowod, ze test patrzy na wlasciwy plik i cos w nim widzi.
        self.assertIn("ctypes", imported)

    def test_wersja_dodatku_rosnie_bo_stary_nie_umie_ukrywac(self):
        manifest = (ROOT / "nvda-addon/addon/manifest.ini").read_text(encoding="utf-8")
        version = next(
            line.split("=", 1)[1].strip()
            for line in manifest.splitlines()
            if line.startswith("version")
        )
        self.assertGreater(
            tuple(int(p) for p in version.split(".")), (0, 3, 3),
            "bez nowej wersji nie da sie odroznic dodatku, ktory umie ukrywac",
        )


if __name__ == "__main__":
    unittest.main()
