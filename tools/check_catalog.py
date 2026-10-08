#!/usr/bin/env python3
"""Offline structural/editorial audit; does not prove source support or translation fluency."""
import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
from urllib.parse import urlsplit

LANGUAGES = {'en', 'zh-Hans', 'zh-Hant', 'ja', 'ko', 'fr', 'de', 'es', 'pt-BR', 'it', 'ru', 'ar', 'hi', 'id', 'tr', 'vi'}
FIELDS = set('id kind category title body keywords sources evidence featureIds destination risk restart restore illustration'.split())
TEXT_FIELDS = {'title', 'body', 'keywords', 'evidence'}
ENUMS = {
    'kind': set('NativeSettings NativeShortcut Guide Historical FeatureFlag'.split()),
    'category': set('ContextMenu Explorer Taskbar Start Windows Input Accessibility Appearance Notifications Performance SystemTools Privacy'.split()),
    'risk': set('None UnsavedWork Files Privacy Power Accessibility Experimental Network Security'.split()),
    'restart': set('None App SignOut Device Varies'.split()),
    'restore': set('None PreviousSetting CloseView Backup Manual'.split()),
    'illustration': set('Context Explorer Taskbar Layout Sound Settings Widgets Search'.split()),
}


def normalize(value):
    return ''.join(c for c in unicodedata.normalize('NFKC', value).casefold() if c.isalnum())


def read_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f'duplicate JSON key {key!r}')
            result[key] = value
        return result
    return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique)


def audit(root):
    errors, warnings = [], []
    catalog = root / 'src/ViVeUI.Core/Catalog'
    summary = {'entries': 0, 'locale_files': 0, 'by_kind': {}, 'by_category': {}}
    def load(path, expected):
        try:
            value = read_json(path)
            if not isinstance(value, expected):
                raise ValueError(f'expected {expected.__name__}')
            return value
        except (OSError, ValueError) as exc:
            errors.append(f'{path.relative_to(root)}: {exc}')
            return expected()
    entries = load(catalog / 'entries.json', list)
    summary['entries'] = len(entries)
    if len(entries) < 200:
        errors.append(f'Expected at least 200 entries; found {len(entries)}')
    # Mirror the executable allowlist, avoiding a second drifting list of URIs.
    core = root / 'src/ViVeUI.Core/CuratedCatalog.cs'
    try:
        source = core.read_text(encoding='utf-8')
        block = source.split('Destinations =', 1)[1].split('}.ToFrozenSet', 1)[0]
        destinations = set(re.findall(r'"([^"\n]+)"', block))
    except (OSError, IndexError):
        destinations = set()
        errors.append('Could not read the core destination allowlist')
    try:
        illustrations = ET.parse(root / 'src/ViVeUI.Windows/Assets/Illustrations.xaml').getroot()
        illustration_keys = {node.attrib.get('{http://schemas.microsoft.com/winfx/2006/xaml}Key') for node in illustrations}
    except (OSError, ET.ParseError) as exc:
        illustration_keys = set()
        errors.append(f'Could not parse packaged illustration resources: {exc}')
    ids, titles, bodies = defaultdict(list), defaultdict(list), defaultdict(list)
    kinds, categories = Counter(), Counter()
    def valid_text(value):
        return isinstance(value, str) and bool(value.strip()) and not any(
            c == '\ufffd' or c in '\u061c\u200e\u200f' or '\u202a' <= c <= '\u202e' or '\u2066' <= c <= '\u2069'
            or unicodedata.category(c) == 'Cc' and c not in '\n\r\t' for c in value)
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            errors.append(f'Entry {index}: expected object'); continue
        ident = entry.get('id', f'index-{index}')
        label = str(ident)
        if set(entry) != FIELDS:
            errors.append(f'{label}: incorrect fields (missing {sorted(FIELDS-set(entry))}, extra {sorted(set(entry)-FIELDS)})')
        if not isinstance(ident, str) or not re.fullmatch(r'[A-Z][A-Za-z0-9]*', ident):
            errors.append(f'{label}: invalid stable ID')
        ids[label.casefold()].append(label)
        for key in TEXT_FIELDS:
            if not valid_text(entry.get(key)):
                errors.append(f'{label}: empty or unsafe {key}')
        for key, target in [('title', titles), ('body', bodies)]:
            if isinstance(entry.get(key), str):
                target[normalize(entry[key])].append(label)
        for key, allowed in ENUMS.items():
            if not isinstance(entry.get(key), str) or entry.get(key) not in allowed:
                errors.append(f'{label}: invalid {key}')
        if str(entry.get('illustration')) + 'Illustration' not in illustration_keys:
            errors.append(f'{label}: illustration resource is not packaged')
        kinds[str(entry.get('kind'))] += 1
        categories[str(entry.get('category'))] += 1
        destination = entry.get('destination')
        if destination is not None and (not isinstance(destination, str) or destination not in destinations):
            errors.append(f'{label}: disallowed destination')
        if entry.get('kind') == 'Historical' and destination is not None:
            errors.append(f'{label}: historical entry must be read-only')
        if entry.get('kind') == 'FeatureFlag':
            errors.append(f'{label}: feature flag has no current-build verification provider')
        refs = entry.get('featureIds')
        if not isinstance(refs, list) or any(type(x) is not int or not 0 < x <= 4294967295 for x in refs):
            errors.append(f'{label}: invalid feature references')
        elif len(refs) != len(set(refs)):
            errors.append(f'{label}: duplicate feature references within entry')
        sources = entry.get('sources')
        if not isinstance(sources, list) or not sources:
            errors.append(f'{label}: missing source URLs'); sources = []
        seen_sources = set()
        for url in sources:
            try:
                if not valid_text(url): raise ValueError('empty or unsafe URL')
                uri = urlsplit(url)
                if uri.scheme != 'https' or not uri.hostname or uri.username or uri.password or uri.port not in (None, 443):
                    raise ValueError('expected HTTPS URL without credentials or custom port')
                if url in seen_sources: raise ValueError('duplicate source URL')
                seen_sources.add(url)
                if not uri.path.strip('/'):
                    errors.append(f'{label}: bare source homepage {url}')
            except (ValueError, TypeError) as exc:
                errors.append(f'{label}: malformed source {url!r}: {exc}')
        body = entry.get('body', '')
        if isinstance(body, str):
            words = len(body.split())
            if words < 12 or words > 100:
                warnings.append(f'{label}: English body has {words} words; review usefulness/length')
            if re.search(r'\b(disable|delete|remove|reset|format|erase)\b', body, re.I) and entry.get('risk') == 'None':
                warnings.append(f'{label}: review destructive/negative wording against risk=None')
    for name, groups in [('ID', ids), ('normalized title', titles), ('normalized body', bodies)]:
        for values in groups.values():
            if len(values) > 1:
                errors.append(f'Duplicate {name}: {", ".join(values)}')
    summary['by_kind'] = dict(sorted(kinds.items()))
    summary['by_category'] = dict(sorted(categories.items()))
    files = sorted((catalog / 'Locales').glob('*.json'))
    summary['locale_files'] = len(files)
    codes = {path.stem for path in files}
    if codes != LANGUAGES:
        errors.append(f'Locale set mismatch: missing={sorted(LANGUAGES-codes)}, extra={sorted(codes-LANGUAGES)}')
    english = {e['id']: e for e in entries if isinstance(e, dict) and isinstance(e.get('id'), str)}
    for path in files:
        locale = load(path, dict)
        if set(locale) != set(english):
            errors.append(f'{path.stem}: ID set mismatch: missing={len(set(english)-set(locale))}, extra={len(set(locale)-set(english))}')
        for ident, text in locale.items():
            if not isinstance(text, dict) or set(text) != TEXT_FIELDS:
                errors.append(f'{path.stem}/{ident}: incorrect text fields'); continue
            for field in TEXT_FIELDS:
                if not valid_text(text[field]):
                    errors.append(f'{path.stem}/{ident}: empty or unsafe {field}')
            original = english.get(ident)
            if original and all(isinstance(text.get(f), str) and isinstance(original.get(f), str) for f in TEXT_FIELDS):
                if path.stem == 'en' and any(text[f] != original[f] for f in TEXT_FIELDS):
                    errors.append(f'en/{ident}: text differs from source entry')
                if path.stem != 'en' and normalize(text['body']) == normalize(original['body']):
                    errors.append(f'{path.stem}/{ident}: English body clone')
                if path.stem != 'en' and normalize(text['evidence']) == normalize(original['evidence']):
                    warnings.append(f'{path.stem}/{ident}: evidence unchanged; review narrative translation')
    summary.update(errors=errors, warnings=warnings, error_count=len(errors), warning_count=len(warnings),
                   scope='Offline structural checks only; source support and translation fluency require editorial review.')
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--report', type=Path, help='Write complete JSON summary to this path')
    parser.add_argument('--max-messages', type=int, default=30, help='Maximum diagnostics printed (default: 30)')
    args = parser.parse_args()
    report = audit(args.root.resolve())
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    printed = {k: v for k, v in report.items() if k not in ('errors', 'warnings')}
    print(json.dumps(printed, ensure_ascii=False, indent=2))
    budget = max(0, args.max_messages)
    for severity in ('errors', 'warnings'):
        for message in report[severity][:budget]:
            print(f'{severity.upper()}: {message}')
        budget = max(0, budget-len(report[severity]))
    if report['error_count'] + report['warning_count'] > max(0, args.max_messages):
        print('Additional diagnostics omitted; use --report for the complete JSON report.')
    return 1 if report['errors'] else 0


if __name__ == '__main__':
    sys.exit(main())
