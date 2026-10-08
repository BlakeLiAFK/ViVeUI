#!/usr/bin/env python3
"""Assemble reviewed domains and complete locale maps; no network or generated evidence."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'catalog-work'
OUT = ROOT / 'src/ViVeUI.Core/Catalog'
FIELDS = ('title', 'body', 'keywords', 'evidence')
# One entry per outcome. A source-linked archive replaces the overlapping native guide.
REPLACED = {
    'TaskbarPosition': 'TaskbarPositionHistory', 'TaskbarCompactHeight': 'SmallTaskbarHistory',
    'StartSections': 'StartCustomization', 'StartSize': 'StartCustomization', 'StartIdentity': 'StartCustomization',
    'StartAllAppsView': 'RedesignedStartRecipe', 'StartExpandedPins': 'RedesignedStartRecipe',
    'ContextMenu': 'ContextMenu', 'Energy': 'Energy', 'StartAppFolders': 'StartFolderCommands',
    'HdrDesktop': 'HdrSettingsHistory', 'AppNotifications': 'PerAppNotifications',
}

def load(path):
    return json.loads(path.read_text(encoding='utf-8'))

def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

def assemble(locales=False):
    entries, chinese = [], {}
    for domain in ('shell', 'access', 'system'):
        entries.extend(e for e in load(WORK/'research'/f'{domain}.json') if e['id'] not in REPLACED)
        chinese.update(load(WORK/'research'/f'{domain}.zh-Hans.json'))
    flags=load(WORK/'research/flags.json')
    chinese.update(load(WORK/'research/flags.zh-Hans.json'))
    for e in flags:
        if e['id']=='EnergyHistory':
            chinese['Energy']=chinese[e['id']]
            e['id']='Energy'
        if e['id'] in ('SettingsDateHistory','SettingsResetHistory'): continue
        if e['id']=='SettingsRenameHistory':
            e.update(id='ModernSettingsDialogs', title='Modern Settings dialog styling',
                body='The historical mapping groups visual updates to Rename PC (38228963), date and time (39811196), and reset (41598133), alongside master ID 36390579. Dependencies and activation order are unknown. This research-only archive does not rename, change the clock, or reset the PC; reset operations can remove apps and data.',
                keywords='modern settings dialogs rename date time reset styling master dependency',
                featureIds=[38228963,39811196,41598133,36390579])
            chinese[e['id']]={
                'title':'现代设置对话框样式',
                'body':'历史映射列出了重命名电脑（38228963）、日期和时间（39811196）、重置（41598133）的界面更新，以及主标志 36390579。依赖关系和启用顺序未知。本研究档案不会执行重命名、更改时钟或重置电脑；实际重置操作可能删除应用与数据。',
                'keywords':'现代 设置 对话框 重命名 日期 时间 重置 样式 主标志 依赖',
                'evidence':'维护者历史映射：Dev 25231。当前设备支持情况与完整配方未经验证。'}
        entries.append(e)
    preferred=['ClassicMenu','ContextMenu','ExplorerTabsHistory','ExplorerNavigationHistory','ExternalFolderTabs','RestoreExplorerTabs','StartCustomization','RedesignedStartRecipe','TaskbarPositionHistory','SmallTaskbarHistory','SmallTaskbarButtonsHistory','ExplorerAiActions']
    order={key:i for i,key in enumerate(preferred)}
    entries.sort(key=lambda e:(order.get(e['id'],len(order)),e['kind']!='Historical',e['category'],e['title']))
    ids=[e['id'] for e in entries]
    assert len(ids)==len(set(ids)) and len(ids)>=200
    save(OUT/'entries.json',entries)
    save(OUT/'Locales/en.json',{e['id']:{k:e[k] for k in FIELDS} for e in entries})
    save(OUT/'Locales/zh-Hans.json',{key:chinese[key] for key in ids})
    save(WORK/'translation-input.json',entries)
    save(WORK/'historical-translation-input.json',[e for e in entries if e['kind']=='Historical'])
    save(WORK/'deduplication.json',{'nativeCandidates':165,'flagCandidates':62,'mergedDialogArchives':3,'replacedNative':REPLACED,'finalEntries':len(entries),'historicalEntries':sum(e['kind']=='Historical' for e in entries),'verifiedActionableFlags':0})
    if locales:
        for code in ('zh-Hant','ja','ko','fr','de','es','pt-BR','it','ru','ar','hi','id','tr','vi'):
            text=load(WORK/'locales'/f'{code}.json')
            missing=set(ids)-text.keys()
            if missing: raise ValueError(f'{code}: missing {sorted(missing)}')
            save(OUT/'Locales'/f'{code}.json',{key:text[key] for key in ids})
    print(f'Assembled {len(entries)} entries; locales={locales}')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--locales',action='store_true');args=parser.parse_args();assemble(args.locales)
