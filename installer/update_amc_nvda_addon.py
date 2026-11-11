"""Install the bundled AMC add-on during a silent AMC update.

Only the amcController directory is replaced.  The previous directory is
retained next to NVDA's addons directory for recovery.
"""

from __future__ import annotations

import csv
import io
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
import zipfile


ADDON_NAME = "amcController"
MAX_UNCOMPRESSED_BYTES = 16 * 1024 * 1024
AMC_MUTEXES = (
    "Local\\AccessibleMultimediaController.PythonLauncher.SingleInstance",
    "Local\\AccessibleMultimediaController.PythonLauncher.Public.SingleInstance",
)


def _manifest_value(contents: str, key: str) -> str:
    match = re.search(rf"(?m)^{re.escape(key)}\s*=\s*([^\r\n]+)", contents)
    return match.group(1).strip().strip('"') if match else ""


def _version_tuple(value: str) -> tuple[int, ...]:
    if not re.fullmatch(r"\d+(?:\.\d+){1,3}", value):
        return ()
    return tuple(int(part) for part in value.split("."))


def _is_newer(installed: str, bundled: str) -> bool:
    old, new = _version_tuple(installed), _version_tuple(bundled)
    if not old or not new:
        return False
    width = max(len(old), len(new))
    return old + (0,) * (width - len(old)) >= new + (0,) * (width - len(new))


def _validate_archive(archive: zipfile.ZipFile, expected_version: str) -> None:
    seen: set[str] = set()
    total_bytes = 0
    files = 0
    for info in archive.infolist():
        name = info.filename
        path = PurePosixPath(name)
        if (
            not name
            or name.startswith("/")
            or "\\" in name
            or ":" in name
            or any(part in ("", ".", "..") for part in path.parts)
            or (info.external_attr >> 16) & 0o170000 == 0o120000
        ):
            raise ValueError(f"Niebezpieczna ścieżka w paczce dodatku: {name!r}")
        if info.is_dir():
            continue
        folded = name.casefold()
        if folded in seen:
            raise ValueError("Paczka dodatku zawiera powtórzoną ścieżkę.")
        seen.add(folded)
        files += 1
        total_bytes += info.file_size
        if files > 100 or total_bytes > MAX_UNCOMPRESSED_BYTES:
            raise ValueError("Paczka dodatku przekracza dozwolony rozmiar.")
    if "installtasks.py" in seen:
        raise ValueError("Cicha instalacja nie obsługuje zadań instalacyjnych NVDA.")
    if "manifest.ini" not in seen:
        raise ValueError("Paczka dodatku nie zawiera manifestu.")
    manifest = archive.read("manifest.ini").decode("utf-8-sig", errors="strict")
    if (_manifest_value(manifest, "name") != ADDON_NAME or
            _manifest_value(manifest, "version") != expected_version):
        raise ValueError("Nazwa lub wersja dodatku nie zgadza się z instalatorem AMC.")
    bad_member = archive.testzip()
    if bad_member:
        raise ValueError(f"Uszkodzony plik w paczce dodatku: {bad_member}")


def _amc_running() -> bool:
    if os.name != "nt":
        return False
    import ctypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.OpenMutexW.argtypes = (ctypes.c_uint32, ctypes.c_int, ctypes.c_wchar_p)
    kernel32.OpenMutexW.restype = ctypes.c_void_p
    for name in AMC_MUTEXES:
        handle = kernel32.OpenMutexW(0x00100000, 0, name)
        if handle:
            kernel32.CloseHandle(handle)
            return True
        if ctypes.get_last_error() != 2:  # ERROR_FILE_NOT_FOUND
            return True
    return False


def _nvda_running() -> bool:
    if os.name != "nt":
        return False
    output = subprocess.check_output(
        ["tasklist", "/FI", "IMAGENAME eq nvda.exe", "/FO", "CSV", "/NH"],
        text=True,
        creationflags=subprocess.CREATE_NO_WINDOW,
    )
    return any(
        row and row[0].casefold() == "nvda.exe"
        for row in csv.reader(io.StringIO(output))
    )


def _nvda_executable() -> Path:
    import winreg

    key_path = r"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\nvda.exe"
    for hive in (winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE):
        try:
            with winreg.OpenKey(hive, key_path) as key:
                candidate = Path(winreg.QueryValueEx(key, "")[0])
                if candidate.is_file():
                    return candidate
        except (FileNotFoundError, OSError):
            continue
    raise FileNotFoundError("Nie znaleziono zainstalowanego NVDA do restartu.")


def install(archive_path: Path, expected_version: str, addons_dir: Path) -> str:
    addons_dir = addons_dir.resolve(strict=True)
    if addons_dir.name.casefold() != "addons" or addons_dir.parent.name.casefold() != "nvda":
        raise ValueError("Nieprawidłowy katalog dodatków NVDA.")
    installed = addons_dir / ADDON_NAME
    if installed.is_symlink() or (addons_dir / f"{ADDON_NAME}.pendingInstall").exists():
        raise ValueError("Dodatek AMC ma już oczekującą instalację lub nietypowy katalog.")
    if (installed / "installTasks.py").exists():
        raise ValueError("Istniejący dodatek wymaga standardowej procedury NVDA.")
    current_manifest = installed / "manifest.ini"
    current = (
        _manifest_value(current_manifest.read_text(encoding="utf-8-sig"), "version")
        if current_manifest.is_file() else ""
    )
    if _is_newer(current, expected_version):
        return "Dodatek AMC do NVDA ma już aktualną wersję."
    if _amc_running():
        raise RuntimeError("AMC nadal działa; aktualizacja NVDA zaczeka na zamknięcie programu.")
    restart_nvda = _nvda_running()
    nvda_exe = _nvda_executable() if restart_nvda else None
    with zipfile.ZipFile(archive_path) as archive:
        _validate_archive(archive, expected_version)
        with tempfile.TemporaryDirectory(prefix="amc-nvda-stage-", dir=addons_dir.parent) as work:
            staged = Path(work) / ADDON_NAME
            archive.extractall(staged)
            staged_manifest = staged / "manifest.ini"
            if _manifest_value(staged_manifest.read_text(encoding="utf-8-sig"), "version") != expected_version:
                raise ValueError("Rozpakowany dodatek ma niewłaściwą wersję.")
            backup = addons_dir.parent / f"amcController-backup-{uuid.uuid4().hex}"
            moved_old = False
            try:
                if installed.exists():
                    installed.replace(backup)
                    moved_old = True
                staged.replace(installed)
                if nvda_exe is not None:
                    # NVDA's installed launcher can require ShellExecute
                    # (UIAccess/elevation); CreateProcess raises WinError 740.
                    os.startfile(str(nvda_exe), arguments="-r")
            except Exception:
                if moved_old:
                    if installed.exists():
                        shutil.rmtree(installed)
                    backup.replace(installed)
                elif installed.exists():
                    shutil.rmtree(installed)
                raise
    return (
        f"Dodatek AMC do NVDA {expected_version} zainstalowany. "
        + ("Uruchomiono ponownie NVDA." if restart_nvda else "NVDA uruchomi go przy następnym starcie.")
        + (f" Kopia poprzedniej wersji: {backup}" if moved_old else "")
    )


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("Użycie: update_amc_nvda_addon.py PACZKA WERSJA KATALOG_DODATKÓW", file=sys.stderr)
        return 2
    try:
        print(install(Path(argv[0]), argv[1], Path(argv[2])))
        return 0
    except Exception as error:
        print(f"Aktualizacja dodatku AMC do NVDA nie powiodła się: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
