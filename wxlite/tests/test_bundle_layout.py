"""Package layout checks on explicit synthetic files; not an executable test."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timedelta, timezone

path = Path(__file__).resolve().parents[1] / "tools" / "build_bundle.py"
spec = importlib.util.spec_from_file_location("lite_bundle_under_test", path)
assert spec is not None and spec.loader is not None
bundle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bundle)


def test_python_package_matches_isolated_runtime_app_path():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder)
        copied = bundle.copy_python_code(target)
        assert (target / "app" / "amc_wx_lite" / "__main__.py").is_file()
        assert (target / "app" / "amc_wx_lite" / "spotify_source.py").is_file()
        assert (target / "app" / "amc_wx_lite" / "tidal_source.py").is_file()
        assert (target / "app" / "amc_wx_lite" / "sonos_source.py").is_file()
        assert (target / "app" / "amc_wx_lite" / "frontier_smart_source.py").is_file()
        assert (
            target / "app" / "amc_wx_lite" / "locales" / "en_legacy.json"
        ).is_file()
        assert (
            target / "app" / "amc_wx_lite" / "locales" / "template.json"
        ).is_file()
        assert "app/amc_wx_lite/locales/en_legacy.json" in copied
        assert (target / "requirements-tidal-test.txt").is_file()
        assert not (target / "AMC-wx-Lite.cmd").exists()


def test_python_package_replaces_stale_locale_resources():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source_package = source / "amc_wx_lite"
        source_locales = source_package / "locales"
        source_locales.mkdir(parents=True)
        (source_package / "__init__.py").write_text("", encoding="utf-8")
        (source_locales / "en.json").write_text('{"language": "en"}', encoding="utf-8")

        target = root / "bundle"
        stale = target / "app" / "amc_wx_lite" / "locales" / "stale.json"
        stale.parent.mkdir(parents=True)
        stale.write_text("{}", encoding="utf-8")

        copied = bundle.copy_python_code(target, source_wxlite=source)

        assert not stale.exists()
        assert (stale.parent / "en.json").read_text(encoding="utf-8") == (
            '{"language": "en"}'
        )
        assert "app/amc_wx_lite/locales/en.json" in copied


def test_public_python_package_contains_frontier_and_omits_disabled_services():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder)
        package = target / "app" / "amc_wx_lite"
        package.mkdir(parents=True)
        for name in bundle.PROFILE_GATED_SERVICE_MODULES:
            (package / name).write_text("stary prywatny modul", encoding="utf-8")

        copied = bundle.copy_python_code(target, bundle.profile_from_id("public"))

        assert "app/amc_wx_lite/gui.py" in copied
        assert (target / "requirements-tidal-test.txt").is_file()
        profile = bundle.profile_from_id("public")
        assert (package / "frontier_smart_source.py").is_file()
        assert not (package / "sonos_source.py").exists()
        for name, capability in bundle.PROFILE_GATED_SERVICE_MODULES.items():
            if capability in profile.capabilities:
                assert (package / name).is_file()
                assert f"app/amc_wx_lite/{name}" in copied
            else:
                assert not (package / name).exists()
                assert f"app/amc_wx_lite/{name}" not in copied


def test_problem_report_endpoint_is_optional_validated_and_isolated_by_profile():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source_package = source / "amc_wx_lite"
        source_package.mkdir(parents=True)
        (source_package / "__init__.py").write_text("", encoding="utf-8")
        endpoints = {
            "test": "https://amc-sygnalista-test.example.workers.dev/v1/report",
            "public": "https://amc-sygnalista.example.workers.dev/v1/report",
        }
        for profile_id, endpoint in endpoints.items():
            source_name = bundle.PROBLEM_REPORT_ENDPOINT_SOURCE_RESOURCES[profile_id]
            (source_package / source_name).write_text(endpoint + "\n", encoding="utf-8")

        for profile_id in ("public", "test"):
            target = root / profile_id
            copied = bundle.copy_python_code(
                target,
                bundle.profile_from_id(profile_id),
                source,
            )
            relative = "app/amc_wx_lite/problem-report-endpoint.txt"
            assert relative in copied
            assert (target / relative).read_text(encoding="utf-8") == endpoints[profile_id] + "\n"

        test_source = bundle.PROBLEM_REPORT_ENDPOINT_SOURCE_RESOURCES["test"]
        (source_package / test_source).write_text(
            "https://user:secret@example.test/v1/report?token=bad\n",
            encoding="utf-8",
        )
        try:
            bundle.copy_python_code(root / "invalid", source_wxlite=source)
        except SystemExit as error:
            assert "prostym adresem HTTPS" in str(error)
        else:
            raise AssertionError("Nieprawidłowy endpoint nie może trafić do paczki")


def test_missing_problem_report_endpoint_removes_stale_packaged_resource():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        (source / "amc_wx_lite").mkdir(parents=True)
        (source / "amc_wx_lite" / "__init__.py").write_text("", encoding="utf-8")
        target = root / "bundle"
        stale = target / "app" / "amc_wx_lite" / bundle.PROBLEM_REPORT_ENDPOINT_RESOURCE
        stale.parent.mkdir(parents=True)
        stale.write_text("https://stary.example/v1/report\n", encoding="utf-8")

        copied = bundle.copy_python_code(target, source_wxlite=source)

        assert not stale.exists()
        assert "app/amc_wx_lite/problem-report-endpoint.txt" not in copied


def test_local_youtube_oauth_configuration_is_never_packaged():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source_package = source / "amc_wx_lite"
        source_package.mkdir(parents=True)
        (source_package / "__init__.py").write_text("", encoding="utf-8")
        client_id = "123456789012-example.apps.googleusercontent.com"
        (source_package / bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE).write_text(
            client_id + "\n",
            encoding="utf-8",
        )
        client_secret = "syntetyczny-sekret-klienta"
        (source_package / bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE).write_text(
            client_secret + "\n",
            encoding="utf-8",
        )

        relative = "app/amc_wx_lite/youtube-oauth-client-id.txt"
        secret_relative = "app/amc_wx_lite/youtube-oauth-client-secret.txt"
        for profile_id in ("test", "public"):
            target = root / profile_id
            package = target / "app" / "amc_wx_lite"
            package.mkdir(parents=True)
            (package / bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE).write_text(
                "stary-identyfikator\n", encoding="utf-8"
            )
            (package / bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE).write_text(
                "stary-sekret\n", encoding="utf-8"
            )

            copied = bundle.copy_python_code(
                target,
                bundle.profile_from_id(profile_id),
                source,
            )

            assert relative not in copied
            assert secret_relative not in copied
            assert not (target / relative).exists()
            assert not (target / secret_relative).exists()


def test_incomplete_youtube_oauth_configuration_is_not_packaged():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source_package = source / "amc_wx_lite"
        source_package.mkdir(parents=True)
        (source_package / "__init__.py").write_text("", encoding="utf-8")
        (source_package / bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE).write_text(
            "123456789012-example.apps.googleusercontent.com\n",
            encoding="utf-8",
        )

        target = root / "test"
        copied = bundle.copy_python_code(
            target,
            bundle.profile_from_id("test"),
            source,
        )

        assert not any("youtube-oauth-client" in item for item in copied)
        assert not (
            target / "app" / "amc_wx_lite" / bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE
        ).exists()
        assert not (
            target
            / "app"
            / "amc_wx_lite"
            / bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE
        ).exists()


def test_public_package_rejects_all_google_login_configuration():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source_package = source / "amc_wx_lite"
        source_package.mkdir(parents=True)
        (source_package / "__init__.py").write_text("", encoding="utf-8")
        (source_package / bundle.YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE).write_text(
            "123456789012-example.apps.googleusercontent.com\n", encoding="utf-8"
        )
        (source_package / bundle.YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE).write_text(
            "https://oauth.example.test/v1/google/token\n", encoding="utf-8"
        )
        (source_package / bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE).write_text(
            "sekret-ktory-nie-moze-trafic-do-paczki\n", encoding="utf-8"
        )

        public_target = root / "public"
        copied = bundle.copy_python_code(
            public_target, bundle.profile_from_id("public"), source
        )
        package = public_target / "app" / "amc_wx_lite"
        for resource in (
            bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE,
            bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE,
            bundle.YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE,
            bundle.YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE,
        ):
            assert not (package / resource).exists()
            assert all(Path(name).name != resource for name in copied)

        test_target = root / "test"
        bundle.copy_python_code(test_target, bundle.profile_from_id("test"), source)
        test_package = test_target / "app" / "amc_wx_lite"
        assert not (test_package / bundle.YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE).exists()
        assert not (test_package / bundle.YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE).exists()


def test_public_gui_imports_with_spotify_and_without_sonos():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder)
        bundle.copy_python_code(target, bundle.profile_from_id("public"))
        environment = dict(os.environ)
        environment["AMC_RELEASE_PROFILE"] = "public"
        # Kopia pakietu ma być pierwsza, ale potomny interpreter nadal
        # potrzebuje tej samej instalacji wxPython co proces testowy. Samo
        # zastąpienie PYTHONPATH ścieżką ``app`` dawało wynik zależny od tego,
        # czy wx było globalnie zainstalowane, a w prywatnym runtime mogło
        # wręcz zaimportować inną, już zainstalowaną kopię AMC.
        environment["PYTHONPATH"] = os.pathsep.join(
            [str(target / "app"), *(entry for entry in sys.path if entry)]
        )

        result = subprocess.run(
            [
                sys.executable,
                "-c",
                (f"import sys; sys.path.insert(0, {str(target / 'app')!r}); "
                    f"sys.path.insert(1, {str(Path(__file__).parent)!r}); "
                    "from test_gui_logic import install_wx_stub; install_wx_stub(); "
                    "import amc_wx_lite.gui as gui; "
                    "assert gui._RELEASE_PROFILE.profile_id == 'public'; "
                    "assert hasattr(gui, 'TidalSource'); "
                    "assert gui.SpotifyPlaylistDialog.__module__ == 'amc_wx_lite.service_playlist_dialog'; "
                    "assert 'amc_wx_lite.spotify_playlist_dialog' not in sys.modules; "
                    "assert 'amc_wx_lite.spotify_source' in sys.modules; "
                    "assert not hasattr(gui, 'SonosPayloadError'); "
                    "assert hasattr(gui, 'FrontierSmartPayloadError')"
                ),
            ],
            cwd=target,
            env=environment,
            capture_output=True,
            text=True,
            timeout=30,
        )
        assert result.returncode == 0, result.stderr or result.stdout


def test_bundle_manifest_profile_is_explicit_and_public_is_restricted():
    public = bundle.profile_from_id("public")
    manifest = bundle.initial_manifest(public, "a" * 40, False)

    assert manifest["releaseProfileSchema"] == 1
    assert manifest["releaseProfile"] == "public"
    assert manifest["displayName"] == "AMC Public"
    assert "displayVersion" in manifest
    assert manifest["installId"] == "AMC.Public.win-x64"
    assert manifest["updateChannel"] == "stable"
    assert manifest["sessions"] == [
        session for session in bundle.profile_from_id("test").session_ids
        if session != "sonos"
    ]
    assert "spotify" in " ".join(manifest["capabilities"]).casefold()
    assert manifest["credentialNamespace"] != (
        bundle.profile_from_id("test").credential_namespace
    )


def test_both_profiles_reject_retired_librespot_component():
    for profile_id in ("public", "test"):
        profile = bundle.profile_from_id(profile_id)
        try:
            bundle.validate_profile_files(
                profile,
                ["host/LibrespotHost/amc_spotify_librespot_host.exe"],
            )
        except SystemExit:
            pass
        else:
            raise AssertionError(f"LibreSpot reached {profile_id}")


def test_public_profile_rejects_any_listed_private_service_module():
    public = bundle.profile_from_id("public")
    for name, capability in bundle.PROFILE_GATED_SERVICE_MODULES.items():
        if capability in public.capabilities:
            bundle.validate_profile_files(public, [f"app/amc_wx_lite/{name}"])
            continue
        try:
            bundle.validate_profile_files(public, [f"app/amc_wx_lite/{name}"])
        except SystemExit as error:
            assert "Publiczny pakiet AMC" in str(error)
        else:
            raise AssertionError(
                f"Publiczny profil nie moze zawierac prywatnego modulu {name}"
            )


def test_file_integrity_contains_hash_and_exact_size_for_every_file():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        (root / "one.bin").write_bytes(b"1234")
        (root / "two.bin").write_bytes(b"abc")
        manifest = {"files": ["one.bin", "two.bin", "one.bin"]}
        bundle.add_file_integrity(manifest, root)
        assert manifest["files"] == ["one.bin", "two.bin"]
        assert set(manifest["fileHashes"]) == set(manifest["files"])
        assert manifest["fileSizes"] == {"one.bin": 4, "two.bin": 3}


def test_build_label_is_user_facing_date_and_source_revision():
    class Result:
        def __init__(self, stdout):
            self.stdout = stdout

    original = bundle.subprocess.run
    try:
        bundle.subprocess.run = lambda args, **kwargs: Result(
            "7C246DBA1234567890ABCDEF1234567890ABCDEF\n"
            if "rev-parse" in args else ""
        )
        label = bundle.build_label(
            datetime(2026, 10, 13, tzinfo=timezone.utc),
            content_id="A1B2C3D4",
        )
    finally:
        bundle.subprocess.run = original
    assert label == (
        "2026.10.13-7c246dba1234567890abcdef1234567890abcdef-a1b2c3d4"
    )


def test_build_time_keeps_one_stable_user_facing_local_label():
    fields = bundle.build_time_fields(
        datetime(2026, 10, 22, 21, 25, 43, tzinfo=timezone(timedelta(hours=2)))
    )

    assert fields == {
        "created": "2026-10-22T19:25:43+00:00",
        "displayVersion": "22.10.2026, 21:25",
    }


def test_source_identity_marks_untracked_source_as_working_copy():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        subprocess.run(
            ["git", "config", "user.email", "test@example.invalid"],
            cwd=root,
            check=True,
        )
        subprocess.run(
            ["git", "config", "user.name", "AMC test"],
            cwd=root,
            check=True,
        )
        tracked = root / "tracked.txt"
        tracked.write_text("zatwierdzone", encoding="utf-8")
        subprocess.run(["git", "add", "tracked.txt"], cwd=root, check=True)
        subprocess.run(["git", "commit", "-qm", "fixture"], cwd=root, check=True)
        (root / "new-source.py").write_text("print('nowe')", encoding="utf-8")

        original_repo = bundle.REPO
        try:
            bundle.REPO = root
            revision, dirty = bundle.git_source_identity()
        finally:
            bundle.REPO = original_repo

        assert len(revision) == 40
        assert dirty is True


def test_source_identity_ignores_only_release_scratch_directories():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        subprocess.run(
            ["git", "config", "user.email", "test@example.invalid"],
            cwd=root,
            check=True,
        )
        subprocess.run(
            ["git", "config", "user.name", "AMC test"],
            cwd=root,
            check=True,
        )
        tracked = root / "tracked.txt"
        tracked.write_text("zatwierdzone", encoding="utf-8")
        subprocess.run(["git", "add", "tracked.txt"], cwd=root, check=True)
        subprocess.run(["git", "commit", "-qm", "fixture"], cwd=root, check=True)
        output = root / ".test-output" / "candidate" / "artifact.bin"
        temporary = root / ".test-tmp" / "probe" / "trace.txt"
        output.parent.mkdir(parents=True)
        temporary.parent.mkdir(parents=True)
        output.write_bytes(b"artifact")
        temporary.write_text("trace", encoding="utf-8")

        original_repo = bundle.REPO
        try:
            bundle.REPO = root
            revision, dirty = bundle.git_source_identity()
        finally:
            bundle.REPO = original_repo

        assert len(revision) == 40
        assert dirty is False


def test_build_label_marks_tracked_changes_as_working_copy():
    assert bundle.build_label(
        datetime(2026, 10, 14, tzinfo=timezone.utc),
        revision="1fe0f900",
        dirty=True,
        content_id="a1b2c3d4",
    ) == "2026.10.14-1fe0f900-a1b2c3d4-robocza"


def test_bundle_identity_changes_when_any_packaged_file_changes():
    first = bundle.bundle_identity({"AMC.exe": "a" * 64})
    second = bundle.bundle_identity({"AMC.exe": "b" * 64})
    assert first != second


def test_refresh_mode_updates_the_visible_build_identity():
    source = path.read_text(encoding="utf-8")
    assert 'manifest["bundleId"] = bundle_identity(manifest["fileHashes"])' in source
    assert 'manifest["buildLabel"] = build_label(' in source
    assert "manifest.update(build_time_fields())" in source


def test_promotion_verifies_full_candidate_and_installed_package():
    promotion = (
        path.parent / "Promote-AMCPythonTestBuild.ps1"
    ).read_text(encoding="utf-8")
    assert "--untracked-files=all" in promotion
    assert "Test-BundleIntegrity -Root $candidateRoot" in promotion
    assert "Test-BundleIntegrity -Root $InstallRoot" in promotion
    assert "$expectedRuntime" in promotion
    assert "runningExecutable" in promotion


def test_production_launcher_is_copied_as_canonical_entry_point():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source, target = root / "publish", root / "bundle"
        source.mkdir()
        (source / "AMC.exe").write_bytes(b"SYNTHETIC_LAUNCHER")
        copied = bundle.copy_launcher(target, source)
        assert copied == ["AMC.exe"]
        assert (target / "AMC.exe").read_bytes() == b"SYNTHETIC_LAUNCHER"


def test_startup_file_selection_covers_interface_launcher_and_host():
    files = [
        "AMC.exe",
        "AMC-Python-Updater.exe",
        "app/amc_wx_lite/__main__.py",
        "runtime/python.exe",
        "runtime/pythonw.exe",
        "runtime/LICENSE.txt",
        "host/amc_lite_host.exe",
        "host/AccessibleMediaController.Core.dll",
        "host/LibrespotHost/amc_spotify_librespot_host.exe",
        "host/LibrespotHost/provenance-amc-host.json",
        "host/readme.md",
    ]
    selected = bundle.select_startup_files(files, "host")
    assert "AMC.exe" in selected
    assert "app/amc_wx_lite/__main__.py" in selected
    assert "runtime/python.exe" in selected
    assert "runtime/pythonw.exe" in selected
    assert "host/amc_lite_host.exe" in selected
    assert "host/AccessibleMediaController.Core.dll" in selected
    assert "host/LibrespotHost/amc_spotify_librespot_host.exe" in selected
    assert "host/LibrespotHost/provenance-amc-host.json" in selected
    assert "AMC-Python-Updater.exe" not in selected
    assert "host/readme.md" not in selected


def test_dependency_licenses_are_not_dropped_from_host_package():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source, target = root / "host", root / "bundle"
        (source / "licenses" / "tempo-engines").mkdir(parents=True)
        (source / "amc_lite_host.exe").write_bytes(b"SYNTHETIC_LAYOUT_FIXTURE_NOT_EXECUTABLE")
        (source / "licenses" / "tempo-engines" / "license.txt").write_text("Synthetic license marker")
        copied, _ = bundle.copy_host(target, source)
        assert (target / "host" / "licenses" / "tempo-engines" / "license.txt").read_text() == "Synthetic license marker"
        assert "host/licenses/tempo-engines/license.txt" in copied


def test_host_can_be_staged_without_touching_running_host_folder():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source, target = root / "source", root / "bundle"
        source.mkdir()
        (source / "amc_lite_host.exe").write_bytes(b"NEW_SYNTHETIC_HOST")
        old_host = target / "host"
        old_host.mkdir(parents=True)
        old_executable = old_host / "amc_lite_host.exe"
        old_executable.write_bytes(b"RUNNING_SYNTHETIC_HOST")

        copied, _ = bundle.copy_host(target, source, "host-next")

        assert old_executable.read_bytes() == b"RUNNING_SYNTHETIC_HOST"
        assert (target / "host-next" / "amc_lite_host.exe").read_bytes() == b"NEW_SYNTHETIC_HOST"
        assert "host-next/amc_lite_host.exe" in copied


def test_host_subfolder_rejects_path_traversal():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        source.mkdir()
        (source / "amc_lite_host.exe").write_bytes(b"SYNTHETIC")
        try:
            bundle.copy_host(root / "bundle", source, "../outside")
        except SystemExit as error:
            assert "bezpieczna nazwa" in str(error)
        else:
            raise AssertionError("folder hosta nie moze wychodzic poza pakiet")


def test_embedded_path_is_patched_in_copy_not_source():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        runtime = root / "source"
        runtime.mkdir()
        text = "python314.zip\n.\nLib/site-packages\n"
        (runtime / "python314._pth").write_text(text)
        bundle.copy_runtime(root / "bundle", runtime)
        assert "../app" in (root / "bundle" / "runtime" / "python314._pth").read_text().splitlines()
        assert (runtime / "python314._pth").read_text() == text


def test_runtime_can_be_reused_in_place_during_bundle_update():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder) / "bundle"
        runtime = target / "runtime"
        runtime.mkdir(parents=True)
        marker = runtime / "python.exe"
        marker.write_bytes(b"sprawdzony-runtime")
        copied = bundle.copy_runtime(target, runtime)
        assert marker.read_bytes() == b"sprawdzony-runtime"
        assert "runtime/python.exe" in copied


def test_pinned_tidal_dependencies_are_copied_into_enabled_profiles():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        target = root / "bundle"
        runtime_site = target / "runtime" / "Lib" / "site-packages"
        source_site = root / "private-venv" / "Lib" / "site-packages"
        runtime_site.mkdir(parents=True)
        source_site.mkdir(parents=True)
        for name in bundle.TIDAL_SHARED_METADATA:
            (runtime_site / name).mkdir()
        for module_name, metadata_name in bundle.TIDAL_TEST_PACKAGES.values():
            module = source_site / module_name
            module.mkdir()
            (module / "__init__.py").write_text("", encoding="utf-8")
            metadata = source_site / metadata_name
            metadata.mkdir()
            (metadata / "METADATA").write_text("test", encoding="utf-8")

        copied = bundle.copy_tidal_test_dependencies(
            target, bundle.profile_from_id("test"), source_site
        )
        assert "runtime/Lib/site-packages/tidalapi/__init__.py" in copied
        assert (runtime_site / "tidalapi-0.8.11.dist-info" / "METADATA").is_file()
        bundle.validate_tidal_test_dependencies(target, bundle.profile_from_id("test"))
        public_target = root / "bundle-public"
        public_site = public_target / "runtime" / "Lib" / "site-packages"
        public_site.mkdir(parents=True)
        for name in bundle.TIDAL_SHARED_METADATA:
            (public_site / name).mkdir()
        public_copied = bundle.copy_tidal_test_dependencies(
            public_target, bundle.profile_from_id("public"), source_site
        )
        assert "runtime/Lib/site-packages/tidalapi/__init__.py" in public_copied
        bundle.validate_tidal_test_dependencies(
            public_target, bundle.profile_from_id("public")
        )


def test_tidal_dependency_gate_rejects_missing_from_both_profiles():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder)
        for profile_name in ("test", "public"):
            profile = bundle.profile_from_id(profile_name)
            try:
                bundle.validate_tidal_test_dependencies(target, profile)
            except SystemExit as error:
                assert "pełny build" in str(error)
            else:
                raise AssertionError(f"{profile_name} bez tidalapi musi odmówić odświeżenia")
        bundle.validate_profile_files(
            bundle.profile_from_id("public"),
            ["runtime/Lib/site-packages/tidalapi/session.py"],
        )


def test_pinned_librespot_source_matches_local_provenance_and_license_inventory():
    provenance = bundle.validate_librespot_package_source()
    assert provenance["sha256"] == bundle.LIBRESPOT_HOST_SHA256
    assert provenance["amcCommit"] == bundle.LIBRESPOT_AMC_COMMIT
    assert provenance["librespotRev"] == bundle.LIBRESPOT_REVISION
    assert provenance["build"] == bundle.LIBRESPOT_BUILD


def test_librespot_provenance_mismatch_refuses_before_writing_package():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        source = root / "source"
        licenses = root / "licenses"
        dependencies = licenses / "librespot-dependencies"
        source.mkdir()
        dependencies.mkdir(parents=True)
        shutil.copy2(
            bundle.LIBRESPOT_HOST_SOURCE / bundle.LIBRESPOT_HOST_BINARY,
            source / bundle.LIBRESPOT_HOST_BINARY,
        )
        provenance = json.loads(
            (
                bundle.LIBRESPOT_HOST_SOURCE / bundle.LIBRESPOT_HOST_PROVENANCE
            ).read_text(encoding="utf-8")
        )
        provenance["sha256"] = "0" * 64
        (source / bundle.LIBRESPOT_HOST_PROVENANCE).write_text(
            json.dumps(provenance), encoding="utf-8"
        )
        (licenses / "librespot-MIT.txt").write_text(
            "MIT License\nCopyright (c) 2015 Paul Lietar\n", encoding="utf-8"
        )
        (dependencies / "INDEX.txt").write_text(
            f"365 paczek\n{bundle.LIBRESPOT_REVISION}\n", encoding="utf-8"
        )
        (dependencies / "inventory.json").write_text(
            json.dumps([{"verified": True}] * 365), encoding="utf-8"
        )

        target = root / "bundle"
        try:
            bundle.copy_librespot_host(target, source_dir=source, licenses_dir=licenses)
        except SystemExit as error:
            assert "Niespojna proweniencja" in str(error)
        else:
            raise AssertionError("Niespojna proweniencja nie moze trafic do paczki")
        assert not target.exists()


def test_full_bundle_requires_tidal_venv_before_cleaning_existing_candidate():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        target = root / "bundle"
        runtime = root / "runtime"
        target.mkdir()
        runtime.mkdir()
        marker = target / "do-not-touch.txt"
        marker.write_text("poprzedni kandydat", encoding="utf-8")
        original_argv = sys.argv
        try:
            sys.argv = [
                str(path), "--out", str(target), "--runtime", str(runtime),
                "--clean",
            ]
            try:
                bundle.main()
            except SystemExit as error:
                assert "--tidal-site-packages" in str(error)
            else:
                raise AssertionError("Missing TIDAL dependency gate")
        finally:
            sys.argv = original_argv
        assert marker.read_text(encoding="utf-8") == "poprzedni kandydat"

def test_full_bundle_omits_retired_librespot_host():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        host = root / "host-publish"
        launcher = root / "launcher-publish"
        updater = root / "updater-publish"
        runtime = root / "runtime-source"
        tidal_site = root / "tidal-venv" / "Lib" / "site-packages"
        unrelated_cwd = root / "unrelated-cwd"
        target = root / "bundle"
        for directory in (host, launcher, updater, runtime, unrelated_cwd):
            directory.mkdir()
        (host / "amc_lite_host.exe").write_bytes(b"SYNTHETIC_LITE_HOST")
        (launcher / "AMC.exe").write_bytes(b"SYNTHETIC_LAUNCHER")
        (updater / "AMC-Python-Updater.exe").write_bytes(b"SYNTHETIC_UPDATER")
        (runtime / "python.exe").write_bytes(b"SYNTHETIC_PYTHON")
        (runtime / "pythonw.exe").write_bytes(b"SYNTHETIC_PYTHONW")
        (runtime / "python314._pth").write_text("python314.zip\n", encoding="utf-8")
        runtime_site = runtime / "Lib" / "site-packages"
        runtime_site.mkdir(parents=True)
        tidal_site.mkdir(parents=True)
        for name in bundle.TIDAL_SHARED_METADATA:
            (runtime_site / name).mkdir()
        for module_name, metadata_name in bundle.TIDAL_TEST_PACKAGES.values():
            module = tidal_site / module_name
            module.mkdir()
            (module / "__init__.py").write_text("", encoding="utf-8")
            metadata = tidal_site / metadata_name
            metadata.mkdir()
            (metadata / "METADATA").write_text("test", encoding="utf-8")

        original_argv = sys.argv
        original_cwd = Path.cwd()
        try:
            sys.argv = [
                str(path),
                "--out", str(target),
                "--host", str(host),
                "--host-subdir", "host-next",
                "--launcher", str(launcher),
                "--updater", str(updater),
                "--runtime", str(runtime),
                "--tidal-site-packages", str(tidal_site),
            ]
            os.chdir(unrelated_cwd)
            assert bundle.main() == 0
        finally:
            os.chdir(original_cwd)
            sys.argv = original_argv

        manifest = json.loads((target / "bundle-manifest.json").read_text(encoding="utf-8"))
        assert "librespotHost" not in manifest
        assert not any("librespot" in path.casefold() for path in manifest["files"])
        assert (target / "host-next" / "amc_lite_host.exe").is_file()


def test_full_public_bundle_omits_librespot_and_has_complete_sizes():
    with tempfile.TemporaryDirectory() as folder:
        root = Path(folder)
        host = root / "host-publish"
        launcher = root / "launcher-publish"
        updater = root / "updater-publish"
        runtime = root / "runtime-source"
        target = root / "public-bundle"
        for directory in (host, launcher, updater, runtime):
            directory.mkdir()
        runtime_site = runtime / "Lib" / "site-packages"
        tidal_site = root / "tidal-venv" / "Lib" / "site-packages"
        runtime_site.mkdir(parents=True)
        tidal_site.mkdir(parents=True)
        for name in bundle.TIDAL_SHARED_METADATA:
            (runtime_site / name).mkdir()
        for module_name, metadata_name in bundle.TIDAL_TEST_PACKAGES.values():
            module = tidal_site / module_name
            module.mkdir()
            (module / "__init__.py").write_text("", encoding="utf-8")
            metadata = tidal_site / metadata_name
            metadata.mkdir()
            (metadata / "METADATA").write_text("test", encoding="utf-8")
        (host / "amc_lite_host.exe").write_bytes(b"SYNTHETIC_LITE_HOST")
        (launcher / "AMC.exe").write_bytes(b"SYNTHETIC_LAUNCHER")
        (updater / "AMC-Python-Updater.exe").write_bytes(b"SYNTHETIC_UPDATER")
        (runtime / "python.exe").write_bytes(b"SYNTHETIC_PYTHON")
        (runtime / "pythonw.exe").write_bytes(b"SYNTHETIC_PYTHONW")
        (runtime / "python314._pth").write_text("python314.zip\n", encoding="utf-8")

        original_argv = sys.argv
        try:
            sys.argv = [
                str(path),
                "--out", str(target),
                "--profile", "public",
                "--tidal-site-packages", str(tidal_site),
                "--host", str(host),
                "--launcher", str(launcher),
                "--updater", str(updater),
                "--runtime", str(runtime),
                "--clean",
            ]

            assert bundle.main() == 0
        finally:
            sys.argv = original_argv

        manifest = json.loads(
            (target / "bundle-manifest.json").read_text(encoding="utf-8")
        )
        assert manifest["releaseProfile"] == "public"
        assert "librespotHost" not in manifest
        assert not any("librespot" in relative.casefold() for relative in manifest["files"])
        forbidden_modules = {
            name for name, capability in bundle.PROFILE_GATED_SERVICE_MODULES.items()
            if capability not in bundle.profile_from_id("public").capabilities
        }
        assert not any(
            Path(relative).name in forbidden_modules
            for relative in manifest["files"]
        )
        public_package = target / "app" / "amc_wx_lite"
        for resource in (
            bundle.YOUTUBE_OAUTH_CLIENT_ID_RESOURCE,
            bundle.YOUTUBE_OAUTH_CLIENT_SECRET_RESOURCE,
            bundle.YOUTUBE_OAUTH_PUBLIC_CLIENT_ID_RESOURCE,
            bundle.YOUTUBE_OAUTH_PUBLIC_BROKER_RESOURCE,
        ):
            assert not (public_package / resource).exists()
            assert not any(
                Path(relative).name == resource for relative in manifest["files"]
            )
        assert set(manifest["fileHashes"]) == set(manifest["files"])
        assert set(manifest["fileSizes"]) == set(manifest["files"])
        for relative in manifest["files"]:
            file_path = target / relative
            assert manifest["fileSizes"][relative] == file_path.stat().st_size
            assert manifest["fileHashes"][relative] == bundle.sha256(file_path)
