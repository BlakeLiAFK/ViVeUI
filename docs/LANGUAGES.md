# Languages

The app includes 16 common UI resource sets covering navigation,
settings, immediate-operation state explanations, history,
update states and actionable error summaries. Original upstream identifiers and
technical diagnostics remain unchanged and are displayed left-to-right.

| Code | Native name | Preferred Windows fonts |
|---|---|---|
| en | English | Segoe UI |
| zh-Hans | 简体中文 | Microsoft YaHei UI |
| zh-Hant | 繁體中文 | Microsoft JhengHei UI |
| ja | 日本語 | Yu Gothic UI, Meiryo |
| ko | 한국어 | Malgun Gothic |
| fr | Français | Segoe UI |
| de | Deutsch | Segoe UI |
| es | Español | Segoe UI |
| pt-BR | Português (Brasil) | Segoe UI |
| it | Italiano | Segoe UI |
| ru | Русский | Segoe UI |
| ar | العربية | Segoe UI, Tahoma |
| hi | हिन्दी | Nirmala UI, Mangal |
| id | Bahasa Indonesia | Segoe UI |
| tr | Türkçe | Segoe UI |
| vi | Tiếng Việt | Segoe UI |

The default follows the operating-system UI culture captured at startup. Explicit
choices persist separately from the resolved language. Unsupported cultures fall
back to English; regional variants use their supported language family. Chinese
script tags take precedence; Taiwan/Hong Kong/Macao select Traditional Chinese.
Portuguese variants use Brazilian Portuguese. Existing saved en/zh-Hans/es
choices remain valid. Every font list ends with Windows Global User Interface
fallback; no Microsoft font files are redistributed.

Arabic mirrors navigation and layout. Feature IDs, upstream identifiers, file
paths, original illustrations and exact privileged change scope retain LTR
direction. Longer translations wrap and can scroll in compact layouts. The
revision has no additional app confirmation dialog. Native renders and RTL state
assertions for its checkbox controls passed the documented Windows workflow;
linguistic and screen-reader review remain separate requirements.

## Maintenance and verification

Edit UTF-8 JSON in `src/ViVeUI.Core/Localization/`. Keep the same keys and numbered
composite placeholders as en.json. Resource loading rejects missing/extra keys,
empty strings, duplicate keys, invalid placeholders and hidden bidi controls.
Add a language definition in `Localization.cs` and extend validation expectations
when adding a language. Do not translate numeric IDs or upstream symbol names.

Run `python3 tools/check_localization.py` and
`dotnet run --project src/ViVeUI.Tests -c Release`. On Windows, run
`pwsh tools/New-WindowsPreviews.ps1 -Run` to reproduce the published fixtures.
It produces isolated native renders and assertions without real feature writes,
publication or a remote workflow. Current language renders and exact Windows validation provenance are recorded in
[previews](previews/README.md); v0.5.0 images remain separately archived.

Translations were authored for this implementation and have **not been certified
by native speakers**. Native review of terminology, grammar, Arabic/Hindi shaping
and accessibility remains required. Automated key coverage and installed glyph
checks are not linguistic approval. Windows renders are offscreen control-tree
renders, not an end-user desktop usability study.

The sourced guide titles, instructions, risks, restore boundaries, visibility filters and operation messages are also localized. Each of the 213 catalog entries has localized title, full body, keywords and narrative evidence in all 16 languages. Exact build/channel identifiers and source URLs remain verbatim technical metadata. Catalog text lives in `Catalog/Locales/`, separate from common UI strings. Simplified Chinese was authored with the research; Traditional Chinese was converted with OpenCC terminology conversion and reviewed for resource integrity. This is not native-speaker certification.
