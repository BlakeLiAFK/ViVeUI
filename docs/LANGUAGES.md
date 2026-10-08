# Languages

Version 0.3.0 embeds 158 resource keys per language, covering navigation,
settings, state explanations, curated feature descriptions, review/confirmation,
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
direction. Privileged confirmations separate ID, before and after into independently
shaped columns so bidi ordering cannot merge state labels. Longer translations wrap and can scroll in compact layouts.
Confirmations always default to the translated Cancel button. Before an elevated
worker authenticates its request, errors use the system language; after
authentication the selected UI language is used.

## Maintenance and verification

Edit UTF-8 JSON in `src/ViVeUI.Core/Localization/`. Keep the same keys and numbered
composite placeholders as en.json. Resource loading rejects missing/extra keys,
empty strings, duplicate keys, invalid placeholders and hidden bidi controls.
Add a language definition in `Localization.cs` and extend validation expectations
when adding a language. Do not translate numeric IDs or upstream symbol names.

Run `python3 tools/check_localization.py` and
`dotnet run --project src/ViVeUI.Tests -c Release`. On Windows, run
`pwsh tools/Test-Localization.ps1` for isolated native renders and assertions.
GitHub Actions runs the same native fixture before packaging both architectures.
It performs no real feature writes, updater download or UAC request.

Translations were authored for this implementation and have **not been certified
by native speakers**. Native review of terminology, grammar, Arabic/Hindi shaping
and accessibility remains required. Automated key coverage and installed glyph
checks are not linguistic approval. Windows renders are offscreen control-tree
renders, not an end-user desktop usability study.
