"""Verify downloaded Windows CI artifacts without executing an EXE.
Usage: python tools/verify_release.py ARTIFACT_ROOT EXACT_COMMIT VERSION
"""
import hashlib
import json
from pathlib import Path
import struct
import sys
import zipfile

root, head, version = Path(sys.argv[1]), sys.argv[2], sys.argv[3]
assets = root / 'ViVeUI-release-assets'
validation = root / 'ViVeUI-native-validation'
def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
build = read_json(assets / 'BUILD.json')
assert build['commit'] == head and build['version'] == version
assert build['singleExe'] and build['selfContained']
checksums = (assets / 'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines()
expected = {'ViVeUI-win-x64.exe', 'ViVeUI-win-arm64.exe', 'ViVeUI-source.zip', 'ViVeUI-licenses.zip'}
verified = {}
for line in checksums:
    digest, name = line.split(None, 1)
    name = name.strip()
    assert name in expected and name not in verified
    payload = (assets / name).read_bytes()
    assert hashlib.sha256(payload).hexdigest() == digest
    verified[name] = {'bytes': len(payload), 'sha256': digest}
assert set(verified) == expected
for arch, machine in [('x64', 0x8664), ('arm64', 0xaa64)]:
    data = (assets / f'ViVeUI-win-{arch}.exe').read_bytes()
    offset = struct.unpack_from('<I', data, 60)[0]
    assert data[offset:offset+4] == b'PE\0\0' and struct.unpack_from('<H', data, offset+4)[0] == machine
with zipfile.ZipFile(assets / 'ViVeUI-source.zip') as source:
    assert source.comment.decode() == head and source.testzip() is None
    names = source.namelist()
    for path in ['LICENSE', 'THIRD-PARTY-NOTICES.md', 'src/ViVeUI.Core/ImmediateToggleController.cs', 'src/ViVeUI.Core/UnifiedCatalog.cs', 'src/ViVeUI.Core/ManualIdInspector.cs', 'src/ViVeUI.Windows/WorkerChannel.cs']:
        assert path in names
    assert any(name.startswith('vendor/ViVe/') for name in names)
    for prefix in ['src/ViVeUI.Core/Localization/', 'src/ViVeUI.Core/Catalog/Locales/']:
        assert sum(name.startswith(prefix) and name.endswith('.json') for name in names) == 16
with zipfile.ZipFile(assets / 'ViVeUI-licenses.zip') as notices:
    assert notices.testzip() is None
    for family in ['windowsdesktop', 'netcore']:
        for arch in ['x64', 'arm64']:
            assert any(family in name.lower() and arch in name.lower() and 'LICENSE' in name for name in notices.namelist())
for name in ['ipc-result.json', 'standalone-ipc-result.json']:
    report = read_json(validation / name)
    assert report['passed'] and report['noFeatureWrites'] and report['rejectedWrongPeer']
    assert report['server']['integrity'] == 8192 and not report['server']['administrator']
    assert report['client']['integrity'] >= 12288 and report['client']['administrator']
locale = read_json(validation / 'localization-result.json')
assert locale['passed'] and locale['noNativeSettingsModified'] and len(locale['reports']) == 16
for report in locale['reports']:
    assert not report['missingSystemGlyphs'] and report['persisted'] and report['statePreserved'] and report['checkboxBindingVerified']
    assert len(list((validation / 'previews/localization' / report['Code']).glob('*.png'))) == report['renders']
ux = read_json(validation / 'ux-result.json')
for key in ['passed', 'noNativeSettingsModified', 'checkboxAutomationToggle', 'checkboxSpaceKey', 'busyReentryBlocked', 'failedWritePreservesState', 'manualUnlistedReadOnly', 'manualKeyboardEnter', 'manualHistoricalReadOnly', 'manualDoesNotChangeCatalogOrFilters', 'updateCancellation']:
    assert ux[key], key
catalog = read_json(validation / 'catalog-result.json')
assert catalog['passed'] and catalog['entryCount'] == 213 and catalog['pageSize'] == 12 and catalog['noNativeSettingsModified']
assert len(catalog['visitedIds']) == len(set(catalog['visitedIds'])) == 213
assert len(catalog['languages']) == 16
assert not read_json(validation / 'catalog-audit.json')['errors']
for name in ['smoke-result.txt', 'standalone-result.txt']:
    assert (validation / name).read_text(encoding='utf-8-sig').startswith('PASS:')
print(json.dumps({'passed': True, 'commit': head, 'version': version, 'assets': verified,
                  'nativeRenders': len(list((validation / 'previews').rglob('*.png'))),
                  'executedDownloadedBinaries': False}, indent=2))
