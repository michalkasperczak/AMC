"""Package layout checks on explicit synthetic files; not an executable test."""
import importlib.util
from pathlib import Path
import tempfile

path = Path(__file__).resolve().parents[1] / "tools" / "build_bundle.py"
spec = importlib.util.spec_from_file_location("lite_bundle_under_test", path)
assert spec is not None and spec.loader is not None
bundle = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bundle)


def test_python_package_matches_isolated_runtime_app_path():
    with tempfile.TemporaryDirectory() as folder:
        target = Path(folder)
        bundle.copy_python_code(target)
        assert (target / "app" / "amc_wx_lite" / "__main__.py").is_file()


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
