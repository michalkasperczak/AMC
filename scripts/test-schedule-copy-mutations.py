"""Discriminating checks of recording schedule duplication; never edits the source tree.

Run in WSL with --stage on /mnt/c, or on Windows with a fresh NTFS folder.
Requires Windows .NET 8 SDK and the same NuGet source as the normal build.
This test opens WPF windows. Reserve the desktop and check recording safety first.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess


def windows(path):
    return str(path) if os.name == 'nt' else subprocess.check_output(['wslpath', '-w', str(path)], text=True).strip()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--powershell', required=True)
    parser.add_argument('--dotnet-root', required=True)
    parser.add_argument('--nuget-source', required=True)
    parser.add_argument('--preflight', required=True, help='Windows PowerShell recording/desktop safety script')
    parser.add_argument('--only', help='Run one mutation plus its green controls')
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    args.stage.mkdir(parents=True, exist_ok=False)
    names = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=repo).decode().split('\0')
    names = sorted({n for n in names if n and (repo / n).is_file()})
    hashes = {n: hashlib.sha256((repo / n).read_bytes()).hexdigest() for n in names}
    (args.stage / 'sources.json').write_text(json.dumps(hashes, indent=2))
    copy = args.stage / 'repo'
    for name in names:
        dest = copy / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(repo / name, dest)
    main_window='src/AccessibleMediaController.Windows/MainWindow.xaml.cs'
    manager='src/AccessibleMediaController.Windows/RadioSchedulesWindow.xaml.cs'
    editor='src/AccessibleMediaController.Windows/RadioScheduleEditorWindow.xaml.cs'
    ui='--schedule-copy'
    mutations=[
        ('green-ui',None,None,None,ui),
        ('copy-enabled',manager,'copy.Enabled = false;','copy.Enabled = true;',ui),
        ('same-id',manager,'copy.Id = Guid.NewGuid().ToString("N");','copy.Id = row.Schedule.Id;',ui),
        ('shared-days',manager,'ActiveDays = [.. schedule.ActiveDays],','ActiveDays = schedule.ActiveDays,',ui),
        ('lost-main-name',main_window,'Name = schedule.Name,','Name = string.Empty,',ui),
        ('lost-editor-name',editor,'Name = ScheduleNameBox.Text.Trim(),','Name = string.Empty,',ui),
        ('name-collision',manager,'if (!used.Contains(candidate)) return candidate;','if (number >= 2) return candidate;',ui),
        ('delete-not-saved',manager,'_schedules.RemoveAll(schedule => schedule.Id == row.Schedule.Id);\n        CommitChanges();','_schedules.RemoveAll(schedule => schedule.Id == row.Schedule.Id);',ui),
        ('wrong-shortcut',manager,'Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D','Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F',ui),
        ('green-final-ui',None,None,None,ui),
    ]
    if args.only:
        selected = [m for m in mutations if m[0] == args.only]
        if len(selected) != 1: raise ValueError('Unknown mutation')
        suite = selected[0][4]
        mutations = [m for m in mutations if m[0] == args.only or (m[1] is None and m[4] == suite)]
    receipts = []
    expected_totals = {}
    for label, name, old, new, suite in mutations:
        gate = subprocess.run([args.powershell, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', args.preflight], capture_output=True)
        (args.stage / (label+'-preflight.txt')).write_bytes(gate.stdout+gate.stderr)
        if gate.returncode: raise RuntimeError('Recording/desktop gate failed; not starting WPF')
        original = (copy / name).read_bytes() if name else None
        try:
            if name:
                assert original is not None and old is not None and new is not None
                text = original.decode('utf-8')
                if text.count(old) != 1:
                    raise RuntimeError('Mutation anchor must match exactly once: ' + label)
                (copy / name).write_bytes(text.replace(old, new).encode('utf-8'))
            ps = args.stage / (label + '.ps1')
            ps.write_text("""$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.Encoding]::UTF8
$env:DOTNET_ROOT='DOTNET'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
$env:RestoreSources='NUGET'
Set-Location 'REPO'
dotnet restore tests/AccessibleMediaController.Windows.SmokeTests --runtime win-x64 -p:NuGetAudit=false --disable-build-servers
if($LASTEXITCODE -ne 0){exit 80}
dotnet build tests/AccessibleMediaController.Windows.SmokeTests -c Release --no-restore --disable-build-servers
if($LASTEXITCODE -ne 0){exit 81}
dotnet run --project tests/AccessibleMediaController.Windows.SmokeTests -c Release --no-build --disable-build-servers -- SUITE
exit $LASTEXITCODE
""".replace("'DOTNET'", "'" + args.dotnet_root + "'").replace("'NUGET'", "'" + args.nuget_source + "'").replace("'REPO'", "'" + windows(copy) + "'").replace("SUITE", suite), encoding='utf-8-sig')
            log = args.stage / (label + '.log')
            with log.open('wb') as stream:
                completed = subprocess.run([args.powershell, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', windows(ps)], stdout=stream, stderr=subprocess.STDOUT)
            text = log.read_text(encoding='utf-8-sig', errors='replace')
            counts = re.search(r'POWIELANIE: ' + r'(\d+) OK / (\d+) BLAD / razem (\d+)', text)
            if not counts:
                raise RuntimeError('Test did not execute completely: ' + str(log))
            passed, failed, total = map(int, counts.groups())
            if suite not in expected_totals:
                expected_totals[suite] = total
            valid = total == expected_totals[suite] and passed + failed == total
            detected = completed.returncode == 1 and failed > 0 if name else completed.returncode == 0 and failed == 0
            receipt = dict(name=label, exit_code=completed.returncode, passed=passed, failed=failed, total=total, valid=valid, expected_result=detected)
            receipts.append(receipt)
            (args.stage / 'results.json').write_text(json.dumps(receipts, indent=2))
            print(json.dumps(receipt), flush=True)
            if not valid or not detected:
                raise RuntimeError('Mutation or green control failed: ' + str(log))
        finally:
            if original is not None:
                (copy / name).write_bytes(original)
    if hashes != {n: hashlib.sha256((repo / n).read_bytes()).hexdigest() for n in names}:
        raise RuntimeError('Source changed during verification')
    print('ALL MUTATIONS DETECTED; SOURCE UNCHANGED', flush=True)


if __name__ == '__main__':
    main()
