# Repository structure

The product/executable is Gengo. Existing `Kotoba.*` project names and namespaces
remain internal identifiers; renaming them is not needed to use or package the app.
For development commands see [Tools used](tools-used.md) and
[Development workflow](development-workflow.md).

## Application: `src/Kotoba.App`

| File | Responsibility |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | Shared theme, application startup, dispatcher error display |
| `MainWindow.xaml` | Search, personal editor, statistics/history and settings layout |
| `MainWindow.xaml.cs` | Startup, search/cancellation, selections, personal actions, history, resources and backup UI |
| `MainWindow.Entry.cs` | Dictionary result layout, all senses/restrictions/other forms, formatted personal results, clipboard text |
| `MainWindow.InlineEntry.cs` | Inline personal-entry drafts, validation, save and reanalysis in Search |
| `MainWindow.Ai.cs` | Provider/account/model Settings, inline AI requests, follow-ups, cancellation and suggestion drafts |
| `MainWindow.Statistics.cs` | Double-click/Enter statistics-to-definition navigation by stable entry identity without recounting |
| `DefinitionClipboard.cs` | Plain-text/HTML clipboard payloads for all definitions |
| `MainWindow.Kanji.cs` | Async Anki cards, source labels and detailed character popup |
| `MainWindow.TextSize.cs` | Persistent reading size and terminology panel |
| `DefinitionFormatting.cs` | Native rich-definition serialization and formatting handlers |
| `RubyAdorner.cs` | Cached passage readings aligned to source text |
| `NaturalRuby.cs` | Headword reading glyph/spacing layout |
| `app.manifest` | Windows application identity and per-monitor DPI awareness |
| `Properties/AssemblyInfo.cs` | Internal access for the executable UI checks |

## Analysis: `src/Kotoba.Core`

`Models.cs` defines dictionary entries/senses, personal entries, tokens, units,
analyses, grammar cards and persistence-facing record types. `AnalysisEngine.cs`
implements literal overrides, dictionary-backed grouping and inflection explanations.
`GrammarEngine.cs` is the authored rule catalog. `LookupSelection.cs` handles
deduplication of the currently open entry. `EntryPresentation.cs` handles short
labels, terminology, other-form filtering and Anki bracket notation.
`WordAssistance.cs` defines the provider interface, bounded context/prompt,
structured response schema and validation, without network/storage dependencies.
`LookupCandidates.cs` combines contextual candidates with exact surface/root
entries at definition-opening time, retaining personal overrides and homographs.

Core has no WPF or direct network/storage dependency. It takes lexicon/morphology
interfaces so behavior can be checked without loading every external resource.

## Storage and integrations: `src/Kotoba.Infrastructure`

| File | Responsibility |
| --- | --- |
| `ReferenceStore.cs` | Exact resource pins/hashes, downloads, staged installation, SQLite word index, caches and classifications |
| `UniDicMorphology.cs` | NMeCab/UniDic adapter and source-offset restoration |
| `KanjiDictionary.cs` | Verified bundled KANJIDIC2 parser and immutable index |
| `Resources/kanjidic2-all.json.zip` | Unchanged pinned source data embedded at build time |
| `PersonalStore.cs` | Personal schema/migrations, entries/settings, event queries, backup/restore validation |
| `AnkiKanjiClient.cs` | Read-only local API, candidate cleaning, exact matches, cache/profile checks |
| `WordAssistants.cs` | Ollama discovery/chat and ChatGPT model catalog/Responses SSE adapters |
| `ChatGptAccounts.cs` | Gengo's own OAuth registrations, OIDC validation, Windows-protected credentials, refresh and sign-out |

## Executable checks: `tests`

`Kotoba.Checks` tests core rules, persistence/recovery and local Anki fixture
behavior. Optional arguments add real installed-reference behavior, full-set Anki
Stats parity and timing. Its explicit `--install`, `--prepare` and `--anki-probe`
modes have different external-data effects; see the development guide.

`Kotoba.UiChecks` starts real WPF against an isolated `.tools/` directory. It
checks rendering/idle activity, selection/copy, ruby, zoom, layouts, character
popups, categories and rich formatting. It modifies only its isolated fixtures.

## Build files and scripts

- `Directory.Build.props`: nullable/implicit usings, current language version,
  deterministic builds and warnings as errors.
- `global.json`: .NET 10 stable SDK selection and roll-forward policy.
- `scripts/build.ps1`: refresh the stable self-contained build and root shortcut;
  normal mode also compiles Debug, while `-Publish` skips that extra compilation.
- `scripts/create-shortcut.ps1`: create/repair the permanent, machine-specific
  root `Gengo.exe.lnk` launcher and its icon.
- `scripts/check.ps1`: core checks and optional installed-reference/parity/WPF runs.
- `scripts/launch.ps1`: open the published snapshot with normal data or an explicit
  isolated directory. It does not rebuild.

## Documentation and generated/private files

[README](../README.md) is the entry point. These `.docs` guides separate use,
internals, resources, integrations, tooling, workflow and future plans. DESIGN is
the agreed specification; IMPLEMENTATION is historical evidence/status.
`THIRD-PARTY-NOTICES.md` and `LICENSES/` accompany redistributed packages.

`artifacts/Gengo/` is the single generated application package, refreshed by each
successful build, with documentation alongside the executable. Per-feature
preview folders are no longer used. The root `Gengo.exe.lnk` is generated and
Git-ignored too. `.tools/` contains project-local build caches, downloaded
fixtures, isolated databases and verification logs. `bin/` and `obj/` are generated.
These generated directories are ignored by Git and are not the user's normal data.

The default live data root is outside the repository at `%LOCALAPPDATA%\Kotoba`.
Do not confuse obsolete application packages with that active old-name data folder.
No personal database, passage history or Anki collection belongs in source control.
