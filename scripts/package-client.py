"""Package an existing Unity Windows build and produce the launcher's release manifest."""
import argparse
import hashlib
import json
import re
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]


def main():
    # Windows terminals/pipes may use a code page that cannot represent the workspace path.
    sys.stdout.reconfigure(errors='backslashreplace')
    sys.stderr.reconfigure(errors='backslashreplace')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True, help='Unique release number, e.g. 1.0.1')
    parser.add_argument('--base-url', required=True, help='HTTPS URL of the directory hosting this release')
    parser.add_argument('--client-dir', type=Path, default=ROOT / 'build/client')
    parser.add_argument('--output-dir', type=Path, default=ROOT / 'build/releases')
    parser.add_argument('--allow-loopback-http', action='store_true')
    args = parser.parse_args()
    if not re.fullmatch(r'\d{1,6}\.\d{1,6}\.\d{1,6}', args.version):
        parser.error('Version must be MAJOR.MINOR.PATCH (numbers only)')
    url = urlsplit(args.base_url)
    if (not url.netloc or url.username or url.password or url.query or url.fragment or
            (url.scheme != 'https' and not (args.allow_loopback_http and url.scheme == 'http'
             and url.hostname in ('localhost', '127.0.0.1', '::1')))):
        parser.error('base-url must be an HTTPS directory URL (loopback HTTP is for testing only)')
    client = args.client_dir.resolve()
    for name in ('TankBattle.exe', 'UnityPlayer.dll', 'TankBattle_Data'):
        path = client / name
        valid = (path.is_dir() and any(path.iterdir())) if name == 'TankBattle_Data' else path.is_file()
        if not valid:
            parser.error(f'Missing Unity build component: {path}. Run scripts/build-client.ps1 first.')
    output = args.output_dir.resolve() / args.version
    if output.exists():
        parser.error(f'{output} already exists. Use a NEW version for every published release.')
    output.parent.mkdir(parents=True, exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix='.package-', dir=output.parent))
    try:
        filename = f'TankBattle-{args.version}-win64.zip'
        archive = stage / filename
        with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as package:
            for path in sorted(client.rglob('*')):
                relative = path.relative_to(client)
                if path.is_symlink():
                    parser.error(f'Symbolic links are not supported: {relative}')
                if any(part.endswith(('_BackUpThisFolder_ButDontShipItWithYourGame', '_BurstDebugInformation_DoNotShip')) for part in relative.parts):
                    continue
                excluded = ('.pdb', '.mdb', '.log', '.tmp', '.temp', '.bak', '.dmp',
                            '.db', '.sqlite', '.sqlite3', '.db-wal', '.db-shm', '.db-journal',
                            '.sqlite-wal', '.sqlite-shm', '.sqlite-journal',
                            '.sqlite3-wal', '.sqlite3-shm', '.sqlite3-journal')
                if path.is_file() and path.suffix.lower() not in excluded and '.log.' not in path.name.lower():
                    package.write(path, relative.as_posix())
        digest = hashlib.sha256()
        with archive.open('rb') as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b''):
                digest.update(block)
        size = archive.stat().st_size
        if size > 4 * 1024 ** 3:
            parser.error('ZIP exceeds the launcher limit of 4 GiB')
        manifest = {'schemaVersion': 1, 'version': args.version,
                    'packageUrl': args.base_url.rstrip('/') + '/' + filename,
                    'sha256': digest.hexdigest(), 'size': size}
        (stage / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        stage.rename(output)
        print(f'Release ready: {output}')
        print(f'Upload {filename} and manifest.json to {args.base_url}')
    finally:
        if stage.exists():
            shutil.rmtree(stage)


if __name__ == '__main__':
    main()
