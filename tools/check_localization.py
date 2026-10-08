#!/usr/bin/env python3
"""Local static audit; no network, publication, Windows rendering, or semantic translation claims."""
import json
import pathlib
import re
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "src/ViVeUI.Core/Localization"
def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise AssertionError(f"Duplicate resource key: {key}")
        result[key] = value
    return result
resources = {p.stem: json.loads(p.read_text(), object_pairs_hook=unique_pairs) for p in RESOURCES.glob("*.json")}
expected = {"en", "zh-Hans", "zh-Hant", "ja", "ko", "fr", "de", "es", "pt-BR", "it", "ru", "ar", "hi", "id", "tr", "vi"}
assert resources.keys() == expected
source = resources["en"]
for code, entries in resources.items():
    assert entries.keys() == source.keys(), code
    for key, value in entries.items():
        assert isinstance(value, str) and value.strip() and "\ufffd" not in value, (code, key)
        assert sorted(re.findall(r"\{\d+\}", value)) == sorted(re.findall(r"\{\d+\}", source[key])), (code, key)
        assert not re.search(r"[\u202a-\u202e\u2066-\u2069]", value), (code, key)
windows = ROOT / "src/ViVeUI.Windows"
references = set()
for path in windows.glob("*.cs"):
    references.update(re.findall(r'\b(?:L|locale)\["([^"\n]+)"\]', path.read_text()))
for path in windows.rglob("*.xaml"):
    text = path.read_text()
    ET.fromstring(text)
    references.update(re.findall(r'\bL\[([A-Za-z0-9_]+)\]', text))
assert references <= source.keys(), references - source.keys()
assert all("{0}" in source[key] for key in ["ReviewCountFormat", "ReviewIdsFormat", "QueueCountFormat"])
assert "SelectedIndex = prefs.Language" not in (windows / "MainWindow.xaml.cs").read_text()
assert "MessageBox.Show" not in (windows / "Program.cs").read_text()
print(f"PASS: {len(resources)} resource files, {len(source)} keys each, {len(references)} literal UI references, placeholders, bidi-control hygiene, XAML parsing, selector migration and localized-dialog wiring.")
print("NOT VERIFIED: Windows rendering, installed glyph coverage/shaping, RTL interaction, or native-speaker translation quality.")
