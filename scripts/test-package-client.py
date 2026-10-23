"""Regression tests for the release packager; no Unity license required."""
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
(ROOT / 'runtime/launcher-tests').mkdir(parents=True, exist_ok=True)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / 'runtime/launcher-tests')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.client = self.root / 'client'
        self.client.mkdir()
        for name in ('TankBattle.exe', 'UnityPlayer.dll', 'TankBattle_Data/data.bin',
                     'TankBattle_Data/StreamingAssets/map.json', 'debug.pdb', 'player.log',
                     'TankBattle_BackUpThisFolder_ButDontShipItWithYourGame/secret.txt',
                     'players.db', 'players.db-wal', 'save.sqlite3', 'save.sqlite3-shm',
                     'copy.tmp', 'old.bak', 'crash.dmp', 'player.log.1', 'debug.mdb'):
            path = self.client / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'test build fixture')

    def package(self, version='1.0.0', base='https://example.com/releases/v1.0.0'):
        return subprocess.run([sys.executable, str(ROOT / 'scripts/package-client.py'),
                               '--version', version, '--base-url', base,
                               '--client-dir', str(self.client), '--output-dir', str(self.root / 'releases')],
                              capture_output=True, text=True)

    def test_archive_manifest_and_exclusions(self):
        result = self.package()
        self.assertEqual(result.returncode, 0, result.stderr)
        release = self.root / 'releases/1.0.0'
        manifest = json.loads((release / 'manifest.json').read_text())
        archive = release / 'TankBattle-1.0.0-win64.zip'
        self.assertEqual(manifest['sha256'], hashlib.sha256(archive.read_bytes()).hexdigest())
        self.assertEqual(manifest['size'], archive.stat().st_size)
        self.assertEqual(manifest['packageUrl'], 'https://example.com/releases/v1.0.0/' + archive.name)
        with zipfile.ZipFile(archive) as package:
            self.assertEqual(set(package.namelist()), {'TankBattle.exe', 'UnityPlayer.dll',
                             'TankBattle_Data/data.bin', 'TankBattle_Data/StreamingAssets/map.json'})

    def test_release_is_immutable(self):
        self.assertEqual(self.package().returncode, 0)
        archive = self.root / 'releases/1.0.0/TankBattle-1.0.0-win64.zip'
        before = archive.read_bytes()
        (self.client / 'TankBattle.exe').write_bytes(b'changed')
        self.assertNotEqual(self.package().returncode, 0)
        self.assertEqual(archive.read_bytes(), before)

    def test_missing_unity_dependency(self):
        (self.client / 'UnityPlayer.dll').unlink()
        self.assertNotEqual(self.package().returncode, 0)
        self.assertFalse((self.root / 'releases/1.0.0').exists())

    def test_required_unity_binaries_must_be_files(self):
        for name in ('TankBattle.exe', 'UnityPlayer.dll'):
            with self.subTest(name=name):
                path = self.client / name
                content = path.read_bytes()
                path.unlink()
                path.mkdir()
                self.assertNotEqual(self.package().returncode, 0)
                self.assertFalse((self.root / 'releases/1.0.0').exists())
                path.rmdir()
                path.write_bytes(content)

    def test_empty_unity_data_rejected(self):
        import shutil
        shutil.rmtree(self.client / 'TankBattle_Data')
        (self.client / 'TankBattle_Data').mkdir()
        self.assertNotEqual(self.package().returncode, 0)
        self.assertFalse((self.root / 'releases/1.0.0').exists())

    def test_unsafe_version(self):
        self.assertNotEqual(self.package(version='../bad').returncode, 0)

    def test_insecure_or_invalid_url(self):
        for url in ('http://example.com', 'https://example.com/file?query=1', 'https://user:password@example.com'):
            with self.subTest(url=url):
                self.assertNotEqual(self.package(base=url).returncode, 0)


if __name__ == '__main__':
    unittest.main()
