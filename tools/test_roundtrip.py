"""Offline synthetic install tests. Never targets a real game directory."""
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / 'dist' / 'hackmud-zh-mod'


def snapshot(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file()}


def run(script, game, success):
    result = subprocess.run(['powershell.exe', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                             '-File', str(PACKAGE / script), '-GameDir', str(game)],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if (result.returncode == 0) != success:
        raise AssertionError(f'{script}: unexpected exit {result.returncode}: {result.stdout!r}')


def main():
    with tempfile.TemporaryDirectory(prefix='hackmud-synthetic-') as temp:
        game = Path(temp) / 'game'
        game.mkdir()
        (game / 'hackmud_win.exe').write_bytes(b'NOT AN EXECUTABLE; SYNTHETIC FIXTURE')
        # The installer reuses, but never owns, an existing compatible loader.
        for rel in ('winhttp.dll', '.doorstop_version', 'doorstop_config.ini',
                    'BepInEx/core/BepInEx.dll', 'BepInEx/core/0Harmony.dll'):
            src = PACKAGE / rel
            dst = game / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            dst.write_bytes(src.read_bytes())
        nested = game / 'hackmud_win_Data' / 'Managed'
        nested.mkdir(parents=True)
        (nested / 'fixture.bin').write_bytes(b'immutable nested fixture')
        before = snapshot(game)
        run('install.ps1', game, True)
        # Retail baseline validation must reject this fake game, not pretend to pass.
        run('verify.ps1', game, False)
        owned = game / 'BepInEx' / 'plugins' / 'hackmud-zh' / 'zh.json'
        original = owned.read_bytes()
        owned.write_bytes(original + b' ')
        tampered = snapshot(game)
        run('uninstall.ps1', game, False)
        assert snapshot(game) == tampered, 'Rejected uninstall changed files'
        owned.write_bytes(original)
        run('uninstall.ps1', game, True)
        assert snapshot(game) == before, 'Recursive hash roundtrip mismatch'
        assert (game / 'BepInEx').exists(), 'Pre-existing loader was removed'
        # Takeover: old plugin files are backed up and restored byte-for-byte.
        plugin = game / 'BepInEx/plugins/hackmud-zh'
        plugin.mkdir(parents=True)
        old = {}
        for name in ('HackmudZh.dll', 'zh.json'):
            old[name] = (plugin / name).read_bytes() if (plugin / name).exists() else (b'old-' + name.encode())
            (plugin / name).write_bytes(old[name])
        run('install.ps1', game, True)
        backup_dirs = list((game / '.hackmud-zh-backups').iterdir())
        assert len(backup_dirs) == 1, 'Takeover backup missing'
        run('uninstall.ps1', game, True)
        for name, data in old.items():
            assert (plugin / name).read_bytes() == data, f'takeover restore mismatch: {name}'
        # A forged receipt pointing outside the dedicated backup subtree must fail closed.
        (plugin / 'HackmudZh.dll').write_bytes(b'current')
        (plugin / 'zh.json').write_bytes(b'current')
        (plugin / 'wordmap.json').write_bytes(b'current')
        run('install.ps1', game, True)
        receipt = game / '.hackmud-zh-receipt.json'
        text = receipt.read_text(encoding='utf-8')
        text = text.replace('.hackmud-zh-backups\\\\', 'BepInEx\\\\core\\\\')
        receipt.write_text(text, encoding='utf-8')
        forged = snapshot(game)
        run('uninstall.ps1', game, False)
        assert snapshot(game) == forged, 'Forged receipt changed files'
    print('PASS: synthetic install, baseline rejection, tamper refusal, loader preservation, takeover backup/restore, forged receipt refusal')
    return 0


if __name__ == '__main__':
    sys.exit(main())
