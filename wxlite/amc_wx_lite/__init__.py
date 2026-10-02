"""AMC-wx-Lite: lekki wariant Accessible Media Controller na wxPython.

Dwie sesje (pliki lokalne i radio internetowe), natywne kontrolki i ISTNIEJACY
silnik audio AMC uruchamiany w malym bezokiennym hoscie .NET.

Uklad pakietu:
  list_model   - lista wirtualna, stabilny wybor po ID
  navigation   - maszyna stanow: sesje, lista <-> odtwarzacz
  shortcuts    - skroty odczytane ze ZRODEL pelnego AMC
  async_gate   - praca w tle, odrzucanie przeterminowanych wynikow
  state_store  - prywatny stan (atomowy zapis), wlasna lista stacji
  host_client  - protokol JSON-lines do amc_lite_host.exe
  gui          - okno wxPython (wymaga pulpitu Windows)

Moduly inne niz ``gui`` NIE importuja wx, dzieki czemu cala logika jest
testowana w WSL bez pulpitu.
"""

__all__ = [
    "async_gate",
    "host_client",
    "list_model",
    "navigation",
    "shortcuts",
    "state_store",
]

__version__ = "0.1.0"
