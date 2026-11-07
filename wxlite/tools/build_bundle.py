#!/usr/bin/env python3
"""Zloz prywatny pakiet AMC-wx-Lite do uruchomienia na Windows.

Co robi: kopiuje kod Pythona, prywatny runtime, zbudowany bezokienny silnik,
aktualizator i AMC.exe do jednego folderu, ktory wystarczy przeniesc na dysk
NTFS i uruchomic.

Czego NIE robi swiadomie:
  * nie instaluje niczego globalnie,
  * nie pobiera runtime ani MSVCP140.dll (prawa redystrybucji VC++ sa bramka
    publikacji - istniejacy runtime wskazujesz przez --runtime),
  * nie dotyka danych, ustawien, stacji ani nagran pelnego AMC.

Uzycie (w WSL albo na Windows):
    python3 tools/build_bundle.py --out /tmp/AMC-wx-Lite --runtime /mnt/c/.../runtime
    python3 tools/build_bundle.py --out /tmp/AMC-wx-Lite --host-subdir host-next
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlsplit

WXLITE = Path(__file__).resolve().parent.parent
REPO = WXLITE.parent
if str(WXLITE) not in sys.path:
    sys.path.insert(0, str(WXLITE))

from amc_wx_lite.release_profile import (  # noqa: E402
    PUBLIC_PROFILE_ID,
    TEST_PROFILE_ID,
    ReleaseProfile,
    profile_from_id,
)
HOST_PUBLISH = (
    REPO
    / "src"
    / "AccessibleMediaController.LiteHost"
    / "bin"
    / "Release"
    / "net8.0-windows10.0.19041.0"
    / "win-x64"
    / "publish"
)
UPDATER_PUBLISH = (
    REPO
    / "src"
    / "AccessibleMediaController.PythonUpdater"
    / "bin"
    / "Release"
    / "net8.0-windows"
    / "win-x64"
    / "publish"
)
LAUNCHER_PUBLISH = (
    REPO
    / "src"
    / "AccessibleMediaController.PythonLauncher"
    / "bin"
    / "Release"
    / "net8.0-windows"
    / "win-x64"
    / "publish"
)
LIBRESPOT_HOST_SOURCE = REPO / "third_party" / "LibrespotHost" / "win-x64"
LIBRESPOT_LICENSES_SOURCE = REPO / "licenses"
LIBRESPOT_HOST_BINARY = "amc_spotify_librespot_host.exe"
LIBRESPOT_HOST_PROVENANCE = "provenance-amc-host.json"
LIBRESPOT_HOST_SHA256 = (
    "0fde62641c16de6e35ac25962b756f868dd7fd36643e71661f849c231d55c15a"
)
LIBRESPOT_HOST_SIZE = 6_595_072
LIBRESPOT_AMC_COMMIT = "4505f325d5eec7df6f520ee156ee79214882df38"
LIBRESPOT_REVISION = "d36f9f1907e8cc9d68a93f8ebc6b627b1bf7267d"
LIBRESPOT_BUILD = (
    "lokalna przypięta kompilacja Release offline dla AMC Test; bez publikacji"
)
PROFILE_GATED_SERVICE_MODULES = {
    "frontier_smart_source.py": "session.frontier-smart",
    "sonos_source.py": "session.sonos",
    "spotify_playlist_dialog.py": "session.spotify",
    "spotify_source.py": "session.spotify",
    "tidal_source.py": "session.tidal",
    "tidal_native_playback.py": "tidal.native-playback",
    "tidal_bts_audio.py": "tidal.native-playback",
    "tidalapi_session_store.py": "tidal.native-playback",
}

# Only these missing packages are copied from a dedicated, pinned venv.
# Existing shared requests/certifi/etc. stay in the supplied AMC runtime.
TIDAL_TEST_PACKAGES = {
    "tidalapi": ("tidalapi", "tidalapi-0.8.11.dist-info"),
    "pyaes": ("pyaes", "pyaes-1.6.1.dist-info"),
    "ratelimit": ("ratelimit", "ratelimit-2.2.1.dist-info"),
    "mpegdash": ("mpegdash", "mpegdash-0.4.1.dist-info"),
    "isodate": ("isodate", "isodate-0.7.2.dist-info"),
    "dateutil": ("dateutil", "python_dateutil-2.9.0.post0.dist-info"),
}
TIDAL_SHARED_METADATA = (
    "requests-2.34.2.dist-info",
    "certifi-2026.10.23.dist-info",
    "charset_normalizer-3.5.2.dist-info",
    "idna-3.20.dist-info",
    "urllib3-2.8.0.dist-info",
    "six-1.17.0.dist-info",
    "typing_extensions-4.16.0.dist-info",
)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_source_identity() -> tuple[str, bool]:
    """Pelna rewizja i stan wszystkich zrodel; artefakty testowe sa pomijane."""

    try:
        revision_result = subprocess.run(
            ["git", "rev-parse", "HEAD"],
            cwd=REPO,
            check=True,
            capture_output=True,
            text=True,
            timeout=10,
        )
        status_result = subprocess.run(
            [
                "git",
                "status",
                "--porcelain",
                "--untracked-files=all",
                "--",
                ".",
                ":(exclude).test-output/**",
                ":(exclude).test-tmp/**",
            ],
            cwd=REPO,
            check=True,
            capture_output=True,
            text=True,
            timeout=10,
        )
        revision = revision_result.stdout.strip().lower()
        dirty = bool(status_result.stdout.strip())
    except (OSError, subprocess.SubprocessError):
        return "", True
    return revision, dirty


def bundle_identity(file_hashes: dict[str, str]) -> str:
    """Krótka tożsamość dokładnej zawartości paczki, nie tylko źródeł."""

    digest = hashlib.sha256()
    for relative, file_digest in sorted(file_hashes.items()):
        digest.update(relative.replace("\\", "/").encode("utf-8"))
        digest.update(b"\0")
        digest.update(file_digest.lower().encode("ascii"))
        digest.update(b"\n")
    return digest.hexdigest()[:8]


def build_label(
    now: datetime | None = None,
    *,
    revision: str | None = None,
    dirty: bool | None = None,
    content_id: str = "",
) -> str:
    """Lokalna data, rewizja i tożsamość dokładnej zawartości paczki."""

    moment = now or datetime.now().astimezone()
    date = moment.strftime("%Y.%m.%d")
    if revision is None or dirty is None:
        detected_revision, detected_dirty = git_source_identity()
        revision = detected_revision if revision is None else revision
        dirty = detected_dirty if dirty is None else dirty
    parts = [date]
    if revision:
        parts.append(revision.lower())
    if content_id:
        parts.append(content_id.lower())
    label = "-".join(parts)
    return f"{label}-robocza" if dirty else label


PROBLEM_REPORT_ENDPOINT_RESOURCE = "problem-report-endpoint.txt"
PROBLEM_REPORT_ENDPOINT_SOURCE_RESOURCES = {
    TEST_PROFILE_ID: "problem-report-endpoint-test.txt",
    PUBLIC_PROFILE_ID: "problem-report-endpoint-public.txt",
}
YOUTUBE_OAUTH_CLIENT_ID_RESOURCE = "youtube-oauth-client-id.txt"
YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE = "youtube-oauth-client-secret.txt"
YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE = "youtube-oauth-public-client-id.txt"
YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE = "youtube-oauth-public-token-broker.txt"


def _read_optional_problem_report_endpoint(
    source_wxlite: Path,
    profile_id: str,
) -> str | None:
    source = (
        source_wxlite
        / "amc_wx_lite"
        / PROBLEM_REPORT_ENDPOINT_SOURCE_RESOURCES[profile_id]
    )
    if not source.is_file():
        return None
    if source.stat().st_size > 2048:
        raise SystemExit("Endpoint Sygnalisty jest zbyt długi.")
    try:
        endpoint = source.read_text(encoding="utf-8").strip()
        parsed = urlsplit(endpoint)
        _ = parsed.port
    except (OSError, UnicodeError, ValueError) as error:
        raise SystemExit("Nie można odczytać endpointu Sygnalisty.") from error
    if (
        not endpoint
        or "\n" in endpoint
        or "\r" in endpoint
        or parsed.scheme.casefold() != "https"
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.query
        or parsed.fragment
        or parsed.path != "/v1/report"
    ):
        raise SystemExit("Endpoint Sygnalisty nie jest prostym adresem HTTPS /v1/report.")
    return endpoint


def copy_python_code(
    target: Path,
    profile: ReleaseProfile | None = None,
    source_wxlite: Path = WXLITE,
) -> list[str]:
    """Skopiuj wyłącznie moduły dozwolone przez profil wydania.

    Pełny AMC Test zachowuje wszystkie moduły. Publiczny AMC nie dostaje kodu
    prywatnych usług tylko dlatego, że wspólne GUI umie go obsłużyć. Przy
    odświeżeniu istniejącej paczki usuwamy dokładnie znane, niedozwolone pliki,
    aby stary kandydat nie zachował ich przez przypadek.
    """

    selected_profile = profile or profile_from_id(TEST_PROFILE_ID)
    report_endpoint = _read_optional_problem_report_endpoint(
        source_wxlite,
        selected_profile.profile_id,
    )
    package = target / "app" / "amc_wx_lite"
    package.mkdir(parents=True, exist_ok=True)
    copied = []
    for source in sorted((source_wxlite / "amc_wx_lite").glob("*.py")):
        required_capability = PROFILE_GATED_SERVICE_MODULES.get(source.name)
        if (
            required_capability is not None
            and required_capability not in selected_profile.capabilities
        ):
            stale = package / source.name
            if stale.is_file():
                stale.unlink()
            continue
        shutil.copy2(source, package / source.name)
        copied.append(f"app/amc_wx_lite/{source.name}")
    source_locales = source_wxlite / "amc_wx_lite" / "locales"
    destination_locales = package / "locales"
    if destination_locales.exists():
        shutil.rmtree(destination_locales)
    if source_locales.is_dir():
        for source in sorted(source_locales.rglob("*.json")):
            relative = source.relative_to(source_locales)
            destination = destination_locales / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            copied.append(
                f"app/amc_wx_lite/locales/{relative.as_posix()}"
            )
    endpoint_destination = package / PROBLEM_REPORT_ENDPOINT_RESOURCE
    if report_endpoint is None:
        if endpoint_destination.is_file():
            endpoint_destination.unlink()
    else:
        endpoint_destination.write_text(report_endpoint + "\n", encoding="utf-8")
        copied.append(f"app/amc_wx_lite/{PROBLEM_REPORT_ENDPOINT_RESOURCE}")
    # Lokalny sekret i identyfikator AMC Test nigdy nie trafiają do paczki.
    for resource in (
        YOUTUBE_OAUTH_CLIENT_ID_RESOURCE,
        YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE,
        YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE,
        YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE,
    ):
        destination = package / resource
        if destination.is_file():
            destination.unlink()
    # AMC-wx-Lite.cmd zostaje w repo jako narzedzie deweloperskie. Nie wolno
    # go dokladac do paczki uzytkownika, bo moglby wybrac obcy interpreter.
    for name in ("requirements-win64.txt", "JAK_TESTOWAC.md"):
        source = source_wxlite / name
        if source.exists():
            shutil.copy2(source, target / name)
            copied.append(name)
    tidal_requirements = target / "requirements-tidal-test.txt"
    if "tidal.native-playback" in selected_profile.capabilities:
        source = source_wxlite / tidal_requirements.name
        if source.is_file():
            shutil.copy2(source, tidal_requirements)
            copied.append(tidal_requirements.name)
    elif tidal_requirements.is_file():
        tidal_requirements.unlink()
    return copied


def copy_launcher(target: Path, launcher_dir: Path) -> list[str]:
    executable = launcher_dir / "AMC.exe"
    if not executable.is_file():
        raise SystemExit(
            f"Nie ma zbudowanego uruchamiacza w {executable}.\n"
            "Zbuduj go: dotnet publish "
            "src/AccessibleMediaController.PythonLauncher -c Release "
            "-r win-x64 --self-contained false"
        )
    target.mkdir(parents=True, exist_ok=True)
    shutil.copy2(executable, target / executable.name)
    return [executable.name]


def copy_host(
    target: Path,
    host_dir: Path,
    host_subdir: str = "host",
) -> tuple[list[str], dict]:
    """Skopiuj silnik. Brak buildu to BLAD, nie cicha atrapa."""
    if not host_dir.exists():
        raise SystemExit(
            f"Nie ma zbudowanego silnika w {host_dir}.\n"
            "Zbuduj go najpierw:\n"
            "  /home/test/dotnet/dotnet publish "
            "src/AccessibleMediaController.LiteHost -c Release -r win-x64 --self-contained false"
        )
    _safe_host_subdir(host_subdir)
    destination = target / host_subdir
    destination.mkdir(parents=True, exist_ok=True)
    copied = []
    hashes = {}
    for source in sorted(host_dir.rglob("*")):
        if source.is_file() and source.suffix.lower() in {".exe", ".dll", ".json", ".pdb", ".txt", ".md"}:
            relative = source.relative_to(host_dir)
            output = destination / relative
            output.parent.mkdir(parents=True, exist_ok=True)
            try:
                shutil.copy2(source, output)
            except PermissionError as error:
                raise SystemExit(
                    f"Nie można podmienić pliku hosta {output}: {error}"
                ) from error
            copied.append(f"{host_subdir}/{relative.as_posix()}")
            if source.suffix.lower() in {".exe", ".dll"}:
                hashes[relative.as_posix()] = sha256(source)
    executable = destination / "amc_lite_host.exe"
    if not executable.exists():
        raise SystemExit(f"W {host_dir} nie ma amc_lite_host.exe - build jest niepelny.")
    return copied, hashes


def _safe_host_subdir(host_subdir: str) -> None:
    if Path(host_subdir).name != host_subdir or host_subdir in {"", ".", ".."}:
        raise SystemExit(
            "Nazwa folderu hosta musi byc pojedyncza bezpieczna nazwa, "
            "np. host albo host-next."
        )


def validate_librespot_package_source(
    source_dir: Path = LIBRESPOT_HOST_SOURCE,
    licenses_dir: Path = LIBRESPOT_LICENSES_SOURCE,
) -> dict:
    """Zweryfikuj przypiety artefakt i materialy przed pierwszym zapisem."""

    binary = source_dir / LIBRESPOT_HOST_BINARY
    provenance_path = source_dir / LIBRESPOT_HOST_PROVENANCE
    mit_license = licenses_dir / "librespot-MIT.txt"
    dependency_licenses = licenses_dir / "librespot-dependencies"
    index_path = dependency_licenses / "INDEX.txt"
    inventory_path = dependency_licenses / "inventory.json"
    required_files = (binary, provenance_path, mit_license, index_path, inventory_path)
    missing = [str(path) for path in required_files if not path.is_file()]
    if missing:
        raise SystemExit(
            "Niepelne materialy przypietego hosta Librespot:\n  "
            + "\n  ".join(missing)
        )

    actual_size = binary.stat().st_size
    actual_sha = sha256(binary)
    if actual_size != LIBRESPOT_HOST_SIZE or actual_sha != LIBRESPOT_HOST_SHA256:
        raise SystemExit(
            "Przypiety host Librespot nie odpowiada zatwierdzonej proweniencji: "
            f"oczekiwano {LIBRESPOT_HOST_SIZE} B i {LIBRESPOT_HOST_SHA256}, "
            f"jest {actual_size} B i {actual_sha}."
        )

    try:
        provenance = json.loads(provenance_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise SystemExit(f"Nie mozna odczytac proweniencji Librespot: {error}") from error
    expected = {
        "binary": LIBRESPOT_HOST_BINARY,
        "sha256": LIBRESPOT_HOST_SHA256,
        "target": "x86_64-pc-windows-msvc",
        "librespotRev": LIBRESPOT_REVISION,
        "librespotVersion": "0.8.0",
        "crtStatic": True,
        "protocolVersion": 1,
        "sessionId": "spotifyLibrespot",
        "amcCommit": LIBRESPOT_AMC_COMMIT,
        "build": LIBRESPOT_BUILD,
        "rustc": "rustc 1.98.1 (48a229cea 2026-09-01)",
    }
    mismatches = [
        f"{name}: oczekiwano {value!r}, jest {provenance.get(name)!r}"
        for name, value in expected.items()
        if provenance.get(name) != value
    ]
    features = str(provenance.get("features", ""))
    if "native-tls" not in features or "rodio-backend" not in features:
        mismatches.append("features: brak native-tls albo rodio-backend")
    if mismatches:
        raise SystemExit(
            "Niespojna proweniencja przypietego hosta Librespot:\n  "
            + "\n  ".join(mismatches)
        )

    license_text = mit_license.read_text(encoding="utf-8")
    if "MIT License" not in license_text or "Paul Lietar" not in license_text:
        raise SystemExit("Licencja MIT Librespot jest niepelna albo niezgodna.")
    index_text = index_path.read_text(encoding="utf-8")
    if LIBRESPOT_REVISION not in index_text or "365 paczek" not in index_text:
        raise SystemExit("Indeks licencji Librespot nie odpowiada przypietej rewizji.")
    try:
        inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise SystemExit(f"Nie mozna odczytac inwentarza licencji: {error}") from error
    if (
        not isinstance(inventory, list)
        or len(inventory) != 365
        or any(
            not isinstance(item, dict) or item.get("verified") is not True
            for item in inventory
        )
    ):
        raise SystemExit(
            "Inwentarz licencji Librespot nie zawiera 365 zweryfikowanych paczek."
        )
    return provenance


def copy_librespot_host(
    target: Path,
    host_subdir: str = "host",
    source_dir: Path = LIBRESPOT_HOST_SOURCE,
    licenses_dir: Path = LIBRESPOT_LICENSES_SOURCE,
) -> tuple[list[str], dict]:
    """Dolacz przypiety host wzgledem katalogu LiteHost, nigdy wzgledem cwd."""

    _safe_host_subdir(host_subdir)
    validate_librespot_package_source(source_dir, licenses_dir)
    copied: list[str] = []

    host_destination = target / host_subdir / "LibrespotHost"
    host_destination.mkdir(parents=True, exist_ok=True)
    for name in (LIBRESPOT_HOST_BINARY, LIBRESPOT_HOST_PROVENANCE):
        shutil.copy2(source_dir / name, host_destination / name)
        copied.append(f"{host_subdir}/LibrespotHost/{name}")

    licenses_destination = target / host_subdir / "licenses"
    licenses_destination.mkdir(parents=True, exist_ok=True)
    shutil.copy2(
        licenses_dir / "librespot-MIT.txt",
        licenses_destination / "librespot-MIT.txt",
    )
    copied.append(f"{host_subdir}/licenses/librespot-MIT.txt")
    dependency_source = licenses_dir / "librespot-dependencies"
    dependency_destination = licenses_destination / "librespot-dependencies"
    shutil.copytree(dependency_source, dependency_destination, dirs_exist_ok=True)
    copied.extend(
        f"{host_subdir}/licenses/librespot-dependencies/{path.relative_to(dependency_source).as_posix()}"
        for path in sorted(dependency_source.rglob("*"))
        if path.is_file()
    )

    executable = f"{host_subdir}/LibrespotHost/{LIBRESPOT_HOST_BINARY}"
    provenance = f"{host_subdir}/LibrespotHost/{LIBRESPOT_HOST_PROVENANCE}"
    license_files = [
        relative for relative in copied if relative.startswith(f"{host_subdir}/licenses/")
    ]
    metadata = {
        "executable": executable,
        "pathFromLiteHostBase": f"LibrespotHost/{LIBRESPOT_HOST_BINARY}",
        "sha256": LIBRESPOT_HOST_SHA256,
        "bytes": LIBRESPOT_HOST_SIZE,
        "provenance": provenance,
        "licenseFiles": license_files,
    }
    return copied, metadata


def copy_runtime(target: Path, runtime: Path) -> list[str]:
    if not runtime.exists():
        raise SystemExit(f"Wskazany runtime nie istnieje: {runtime}")
    destination = target / "runtime"
    # Przy aktualizacji pakietu na miejscu wskazany, juz sprawdzony runtime
    # moze byc zarazem katalogiem docelowym. Nie kasujemy wtedy zrodla;
    # wpisujemy tylko jego istniejace pliki do nowego manifestu.
    if runtime.resolve() == destination.resolve():
        return [
            p.relative_to(target).as_posix()
            for p in sorted(destination.rglob("*"))
            if p.is_file()
        ]
    if destination.exists():
        shutil.rmtree(destination)
    shutil.copytree(runtime, destination)
    for paths_file in destination.glob("python*._pth"):
        lines = paths_file.read_text(encoding="utf-8").splitlines()
        if "../app" not in lines:
            lines.append("../app")
        paths_file.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return [p.relative_to(target).as_posix() for p in sorted(destination.rglob("*")) if p.is_file()]


def copy_tidal_test_dependencies(
    target: Path, profile: ReleaseProfile, source_site_packages: Path | None,
) -> list[str]:
    """Add the pinned player to an isolated bundle with native TIDAL enabled.

    Never install into the supplied runtime: it may belong to a running AMC.
    A full build must name the isolated venv explicitly; refresh-only
    builds instead require these packages already present in the bundle.
    """
    if "tidal.native-playback" not in profile.capabilities:
        return []
    destination = target / "runtime" / "Lib" / "site-packages"
    if source_site_packages is None:
        raise SystemExit("Odtwarzacz TIDAL wymaga --tidal-site-packages z osobnego venv.")
    validate_tidal_dependency_sources(destination, source_site_packages)
    copied: list[str] = []
    for module_name, metadata_name in TIDAL_TEST_PACKAGES.values():
        source_module = source_site_packages / module_name
        source_metadata = source_site_packages / metadata_name
        if not source_module.is_dir() or not source_metadata.is_dir():
            raise SystemExit("Brakuje przypiętej zależności TIDAL w prywatnym venv.")
        for source in (source_module, source_metadata):
            output = destination / source.name
            if output.exists():
                raise SystemExit("Runtime AMC zawiera już składnik TIDAL; sprawdź jego wersję.")
            shutil.copytree(source, output)
            copied.extend(
                p.relative_to(target).as_posix()
                for p in sorted(output.rglob("*")) if p.is_file()
            )
    return copied


def validate_tidal_dependency_sources(
    runtime_site_packages: Path, source_site_packages: Path,
) -> None:
    if any(not (runtime_site_packages / name).is_dir() for name in TIDAL_SHARED_METADATA):
        raise SystemExit("Runtime AMC Test nie zawiera zgodnych wspólnych zależności TIDAL.")
    if any(
        not (source_site_packages / name).is_dir()
        for pair in TIDAL_TEST_PACKAGES.values() for name in pair
    ):
        raise SystemExit("Prywatny venv nie zawiera przypiętych zależności TIDAL.")


def validate_tidal_test_dependencies(target: Path, profile: ReleaseProfile) -> None:
    """Reject a Python-only refresh when the installed Test runtime lacks TIDAL."""
    if "tidal.native-playback" not in profile.capabilities:
        return
    root = target / "runtime" / "Lib" / "site-packages"
    if any(
        not (root / name).is_dir()
        for pair in TIDAL_TEST_PACKAGES.values() for name in pair
    ):
        raise SystemExit("Istniejąca paczka AMC nie zawiera zależności TIDAL; wykonaj pełny build.")


def copy_updater(target: Path, updater_dir: Path) -> list[str]:
    executable = updater_dir / "AMC-Python-Updater.exe"
    if not executable.is_file():
        raise SystemExit(
            f"Nie ma zbudowanego aktualizatora w {executable}.\n"
            "Zbuduj go: dotnet publish "
            "src/AccessibleMediaController.PythonUpdater -c Release "
            "-r win-x64 --self-contained false"
        )
    shutil.copy2(executable, target / executable.name)
    return [executable.name]


def select_startup_files(files: list[str], host_subdir: str) -> list[str]:
    """Wybierz pliki sprawdzane przed kazdym startem AMC.exe."""
    return [
        relative
        for relative in files
        if (
            relative == "AMC.exe"
            or relative.startswith("app/amc_wx_lite/")
            or relative in {"runtime/python.exe", "runtime/pythonw.exe"}
            or (
                relative.startswith(f"{host_subdir}/")
                and Path(relative).suffix.lower() in {".exe", ".dll", ".json"}
            )
        )
    ]


def profile_allows_librespot(profile: ReleaseProfile) -> bool:
    """Dedykowany host Spotify trafia tylko do profilu, który go udostępnia."""

    return "session.spotify" in profile.capabilities


def validate_profile_files(profile: ReleaseProfile, files: list[str]) -> None:
    """Odmow niejawnego pozostawienia prywatnej zaleznosci w publicznej paczce."""

    forbidden_names = {
        name.casefold()
        for name, capability in PROFILE_GATED_SERVICE_MODULES.items()
        if capability not in profile.capabilities
    }
    forbidden = [
        relative
        for relative in files
        if (
            (
                not profile_allows_librespot(profile)
                and (
                    "/librespothost/"
                    in ("/" + relative.replace("\\", "/")).casefold()
                    or Path(relative).name.casefold()
                    == LIBRESPOT_HOST_BINARY.casefold()
                )
            )
            or Path(relative).name.casefold() in forbidden_names
            or (
                "tidal.native-playback" not in profile.capabilities
                and relative.replace("\\", "/").casefold().startswith(
                    "runtime/lib/site-packages/tidalapi"
                )
            )
        )
    ]
    if forbidden:
        raise SystemExit(
            "Publiczny pakiet AMC zawiera niedozwolone składniki prywatnych usług. "
            "Wykonaj pelne czyste zlozenie profilu publicznego; znaleziono: "
            + ", ".join(forbidden[:5])
        )


def add_file_integrity(manifest: dict, target: Path) -> None:
    """Zapisz hash i rozmiar kazdego jawnie wymienionego pliku paczki."""

    files = list(dict.fromkeys(manifest["files"]))
    manifest["files"] = files
    manifest["fileHashes"] = {
        relative: sha256(target / Path(relative))
        for relative in files
    }
    manifest["fileSizes"] = {
        relative: (target / Path(relative)).stat().st_size
        for relative in files
    }


def build_time_fields(moment: datetime | None = None) -> dict[str, str]:
    """Zapisz razem techniczny czas UTC i stałą etykietę użytkową."""

    local_moment = moment if moment is not None else datetime.now().astimezone()
    if local_moment.tzinfo is None:
        raise ValueError("Czas paczki musi zawierać strefę czasową")
    return {
        "created": local_moment.astimezone(timezone.utc).isoformat(timespec="seconds"),
        "displayVersion": local_moment.strftime("%d.%m.%Y, %H:%M"),
    }


def initial_manifest(
    profile: ReleaseProfile,
    source_revision: str,
    source_dirty: bool,
) -> dict[str, object]:
    """Utworz jawny, testowalny kontrakt paczki dla jednego profilu."""

    manifest: dict[str, object] = {
        "bundleFormatVersion": 1,
        "interfaceVersion": 1,
        "hostProtocolVersion": 1,
        # Zgodnosc przejsciowa z walidatorem obecnego launchera. O tozsamosci
        # uzytkowej i instalacyjnej decyduja pola profilu ponizej.
        "application": "AMC Python",
        "entryPoint": "AMC.exe",
        "pythonModule": "amc_wx_lite",
        "note": (
            "Pakiet AMC bez danych użytkownika. Zawartość runtime zależy od "
            "wskazanego źródła; publikacja wymaga sprawdzenia licencji."
        ),
        "sourceRevision": source_revision,
        "sourceDirty": source_dirty,
        "files": [],
    }
    manifest.update(build_time_fields())
    manifest.update(profile.manifest_fields())
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description="Zloz pakiet AMC-wx-Lite")
    parser.add_argument("--out", required=True, type=Path, help="folder docelowy")
    parser.add_argument(
        "--profile",
        choices=("test", "public"),
        default=None,
        help="profil wydania; dla pelnej paczki domyslnie test",
    )
    parser.add_argument("--host", type=Path, default=HOST_PUBLISH, help="folder z publish silnika")
    parser.add_argument(
        "--host-subdir",
        default="host",
        help=(
            "folder silnika w pakiecie; host-next pozwala przygotowac nowa "
            "wersje bez zamykania uruchomionego hosta"
        ),
    )
    parser.add_argument(
        "--launcher",
        type=Path,
        default=LAUNCHER_PUBLISH,
        help="folder publish produkcyjnego AMC.exe",
    )
    parser.add_argument(
        "--updater",
        type=Path,
        default=UPDATER_PUBLISH,
        help="folder publish wykonywalnego aktualizatora AMC Python",
    )
    parser.add_argument(
        "--runtime",
        type=Path,
        default=None,
        help="prywatny runtime Pythona do dolozenia (nie trafia do Git)",
    )
    parser.add_argument(
        "--tidal-site-packages",
        type=Path,
        default=None,
        help="site-packages przypiętego, osobnego venv TIDAL tylko dla AMC Test",
    )
    parser.add_argument("--clean", action="store_true", help="wyczysc folder docelowy")
    parser.add_argument(
        "--refresh-python-only",
        action="store_true",
        help=(
            "odswiez tylko interfejs, dokumentacje i aktualizator w istniejacej "
            "paczce; nie dotykaj uruchomionego hosta ani runtime"
        ),
    )
    args = parser.parse_args()

    target: Path = args.out
    if args.refresh_python_only:
        manifest_path = target / "bundle-manifest.json"
        if not manifest_path.is_file():
            raise SystemExit(
                "Tryb --refresh-python-only wymaga istniejacej kompletnej paczki"
            )
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        existing_profile_id = manifest.get("releaseProfile", TEST_PROFILE_ID)
        if args.profile is not None and args.profile != existing_profile_id:
            raise SystemExit(
                "Tryb --refresh-python-only nie moze zmienic profilu istniejacej paczki"
            )
        profile = profile_from_id(existing_profile_id)
        validate_tidal_test_dependencies(target, profile)
        manifest.update(profile.manifest_fields())
        files = list(manifest.get("files") or [])
        files += copy_python_code(target, profile)
        files += copy_launcher(target, args.launcher)
        files += copy_updater(target, args.updater)
        source_revision, source_dirty = git_source_identity()
        manifest.update(build_time_fields())
        manifest["files"] = [
            relative
            for relative in dict.fromkeys(files)
            if (target / Path(relative)).is_file()
        ]
        validate_profile_files(profile, manifest["files"])
        add_file_integrity(manifest, target)
        manifest["sourceRevision"] = source_revision
        manifest["sourceDirty"] = source_dirty
        manifest["bundleId"] = bundle_identity(manifest["fileHashes"])
        manifest["buildLabel"] = build_label(
            revision=source_revision,
            dirty=source_dirty,
            content_id=manifest["bundleId"],
        )
        manifest["startupFiles"] = select_startup_files(
            manifest["files"],
            manifest.get("hostFolder", "host"),
        )
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        print(f"Odswiezono interfejs i aktualizator: {target}")
        print(f"  plikow: {len(manifest['files'])}")
        return 0

    if args.runtime is None:
        raise SystemExit(
            "Pelna paczka AMC Python wymaga prywatnego runtime. "
            "Wskaz jego folder przez --runtime."
        )

    profile = profile_from_id(args.profile or TEST_PROFILE_ID)

    # Bramka jest przed --clean i przed pierwsza kopia: niespojna binarka albo
    # proweniencja nie moze nawet czesciowo przebudowac katalogu kandydata.
    # Publiczny profil nie uzywa Spotify, wiec nie wymaga ani nie odczytuje
    # jego prywatnego hosta oraz setek plikow licencji zaleznosci.
    if profile_allows_librespot(profile):
        validate_librespot_package_source()
    if "tidal.native-playback" in profile.capabilities:
        if args.tidal_site_packages is None:
            raise SystemExit("Odtwarzacz TIDAL wymaga --tidal-site-packages z osobnego venv.")
        validate_tidal_dependency_sources(
            args.runtime / "Lib" / "site-packages", args.tidal_site_packages
        )
    elif any(
        (args.runtime / "Lib" / "site-packages" / name).exists()
        for name in ("tidalapi", "tidalapi-0.8.11.dist-info")
    ):
        raise SystemExit("Runtime bez odtwarzacza TIDAL nie może zawierać tidalapi.")

    if args.clean and target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True, exist_ok=True)

    source_revision, source_dirty = git_source_identity()
    manifest: dict = initial_manifest(profile, source_revision, source_dirty)

    manifest["files"] += copy_python_code(target, profile)
    manifest["files"] += copy_launcher(target, args.launcher)
    manifest["files"] += copy_updater(target, args.updater)
    host_files, host_hashes = copy_host(target, args.host, args.host_subdir)
    manifest["files"] += host_files
    if profile_allows_librespot(profile):
        librespot_files, librespot_metadata = copy_librespot_host(
            target,
            args.host_subdir,
        )
        manifest["files"] += librespot_files
        host_hashes[f"LibrespotHost/{LIBRESPOT_HOST_BINARY}"] = LIBRESPOT_HOST_SHA256
        manifest["librespotHost"] = librespot_metadata
    manifest["hostHashes"] = host_hashes
    manifest["hostFolder"] = args.host_subdir

    manifest["files"] += copy_runtime(target, args.runtime)
    manifest["files"] += copy_tidal_test_dependencies(
        target, profile, args.tidal_site_packages
    )
    manifest["runtime"] = "embedded"

    # Aktualizator akceptuje wyłącznie kompletne paczki. Sumy obejmują każdy
    # plik wymieniony w manifeście, nie tylko binaria hosta.
    validate_profile_files(profile, manifest["files"])
    add_file_integrity(manifest, target)
    manifest["bundleId"] = bundle_identity(manifest["fileHashes"])
    manifest["buildLabel"] = build_label(
        revision=source_revision,
        dirty=source_dirty,
        content_id=manifest["bundleId"],
    )
    manifest["startupFiles"] = select_startup_files(
        manifest["files"],
        args.host_subdir,
    )

    (target / "bundle-manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    print(f"Pakiet gotowy: {target}")
    print(f"  plikow: {len(manifest['files'])}")
    for name, digest in host_hashes.items():
        print(f"  {name} sha256={digest}")
    print("\nUruchomienie na Windows:")
    print(r"  AMC.exe --sprawdz-pakiet   (sprawdzenie integralnosci)")
    print(r"  AMC.exe --sprawdz          (diagnostyka interfejsu)")
    print(r"  AMC.exe                    (okno)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
