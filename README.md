# Gengo / 言語 / げんご — personal Japanese–English dictionary

A standalone Windows desktop dictionary for reading Japanese passages. This is
the first runnable implementation of [.docs/DESIGN.md](.docs/DESIGN.md), with
prototype built-in grammar coverage. Dictionary lookup is independent of Anki;
optional kanji cards read your local Anki deck through AnkiConnect.

## Documentation

- [User's guide](.docs/USER-GUIDE.md) — installation, all four tabs, personal
  categories/formatting, shortcuts, history, backups and troubleshooting.
- [How the program works](.docs/program-guide.md) — what happens under the hood,
  analysis flow, rendering, action counting, storage and recovery.
- [Dictionary and analysis](.docs/dictionary-and-analysis.md) — resource pins,
  matching, Common/JLPT parity, word forms and actual grammar coverage.
- [AnkiConnect integration](.docs/anki-connect.md) — setup, read-only API calls,
  exact kanji matching, caching and failures.
- [AI word assistance](.docs/AI-ASSISTANCE.md) — optional ChatGPT/Ollama setup,
  inline context checks and identification, data handling and limitations.
- [Repository structure](.docs/repository-structure.md) — code/file responsibilities,
  checks, scripts, generated output and private data.
- [Tools used](.docs/tools-used.md) — stack, dependencies, prerequisites and commands.
- [Development workflow](.docs/development-workflow.md) — branch rules, isolated
  fixtures, validation and local packaging.
- [Current design](.docs/DESIGN.md) — agreed behavior and acceptance criteria.
- [Implementation record](.docs/IMPLEMENTATION.md) — historical revisions,
  measurements, verification evidence and current gaps.
- [Future plans](.docs/FUTURE-PLANS.md) — unfinished milestones and deferred features,
  separated from what is implemented.

The published folder also includes this README and the `.docs` guides, so the
documentation is available alongside the app without an internet connection.

## Run

Open the permanent **Gengo.exe** shortcut in the project root (the file is
`Gengo.exe.lnk`). It launches the latest successful build from
`artifacts/Gengo/Gengo.exe`. Both normal `scripts/build.ps1` and `-Publish` builds
refresh this one folder and recreate the shortcut with Gengo's icon. Close Gengo
before building. Old per-feature preview folders are no longer used. Build output
and the machine-specific shortcut are Git-ignored; only source/resources/scripts
belong in the repository.
The folder includes its .NET runtime; keep the DLLs and `LICENSES` with the app.
Gengo is the new name for Kotoba. Existing data remains in the original
`%LOCALAPPDATA%/Kotoba` folder, so entries, history, settings and installed resources
carry over automatically. The `KOTOBA_DATA_DIR` override and backup format also
remain compatible.
On first launch, open **Settings → Install offline resources**. The pinned
resources download about 62 MiB and require about 500 MiB installed, including
staged source archives. After installation, ordinary dictionary and built-in
grammar lookups work offline. Downloads have fixed public URLs and pinned SHA-256
checksums. Internet access is only used for explicit setup/manual resource checks.

Paste Japanese text, press **Enter** (or Search), and click a word. Shift+Enter
inserts a line break; Enter confirming Japanese IME composition does not search.
There is
no automatic analysis while typing and no automatic definition opening. Ctrl+L
focuses the input. In the analyzed passage, drag to select/copy text, or right-click
for **Look up selection** and **Add personal entry**. Word units are spaced,
underlined and colored by part of speech. Display-only gaps are omitted from
copy and selection lookup. Search stays pinned above a bounded passage and
side-by-side Words/Kanji results; long definitions scroll within the Words pane.
Keyboard users can focus
the passage, move its caret with arrows, and press Enter to open that word.

Use **Ctrl+Plus** / **Ctrl+Minus** (including numpad +/−) to increase/decrease
reading size in 2-point steps. The setting persists and applies to the input,
passage, dictionary definitions and kanji/grammar results. The Settings slider
controls the same size. Large text switches entries to a stacked layout when
needed. Spelling and senses are selectable; **ANKI card copy** copies the selected
root as `巨大[きょだい]`, ready for an Anki Front field. Kana-only entries omit a
redundant bracketed reading. This button copies text without creating an Anki note.

Common vocabulary has a green badge and JLPT levels have blue badges beneath
the word. **Settings → Dictionary terminology** explains labels such as
futsuumeishi (ordinary noun) and keiyōdōshi (na-adjective). Results use short
English labels, with the original source wording available on hover.

Choose a word containing kanji to see your Anki meanings on the right; click a
kanji card for its popup. Keep Anki open with AnkiConnect enabled. Settings has
the deck/field mapping: defaults **漢字**, **Front**, **Back**, including child
decks. Only exact single-character cards match: 床屋 cannot supply the meaning
of 床. Multiple exact notes retain their stored meanings. This is read-only;
no Anki cards or collection files are modified. Missing/unavailable Anki leaves
dictionary lookup working and shows a status in the kanji card. The in-memory
cache is bounded, profile-specific, and cleared by Save Anki mapping / refresh.

Kanji popups also show independent **KANJIDIC2** dictionary meanings, Kun/On
readings, name readings (nanori), strokes and school grade where recorded. This
13,108-character resource is bundled with the app and needs no additional download
or word-dictionary reinstall. It works even when Anki has no matching card or is
closed. The Anki keyword stays separate. A missing dictionary entry is reported
separately; dots in Kun readings mark okurigana. Readings are character-level
possibilities, not a claim that all readings apply to the selected word.

HTML-wrapped fronts such as `<div>分</div>` are supported: candidate searches are
broad, but the cleaned field must still match the single kanji exactly. A missing
card displays **unknown** and can still open a popup. Misses are not cached, so a
newly added card can appear on the next selection. Service failures instead show
Anki unavailable. Explain grammar sits below the passage legend. Dictionary ruby
keeps natural glyph proportions, using spacing rather than font stretching.

The **Personal dictionary** applies literal expression overrides globally, with
an expression-only list beside the editor. Both start at the top and scroll
independently; the reading remains available when editing an entry. The longest
matching expression takes priority. Save reanalyzes the current passage
without adding a search or click event. **Explain grammar** uses a small offline
rule catalog; unsupported and uncertain constructions remain possible.

Personal entries have **Personal words** or **Names** categories. Use the filters
above the list to browse either category or All, and the Category controls in the
editor to assign or move an entry. Both categories participate in passage lookup.
Existing entries default to Personal words; names can be reassigned individually.
The definition toolbar supports bold (Ctrl+B), italic (Ctrl+I), numbered/bulleted
lists, subscript, and smaller inline usage notes. Select text and click a formatting
button, or toggle the format before typing. Formatting appears in search results
and survives backup/restore. Existing plain-text entries and older backups remain
readable. The schema upgrade first saves a `before-schema-2-*.sqlite` snapshot
beside the personal database. Do not open the upgraded database with an older build.

Statistics require five deliberate definition openings within the selected
local-calendar dates. Reclicking the open entry does not increment it; A → B → A
does. Searches, grammar, scrolling and inspecting history do not count. Search
history is paged. Deleting a passage preserves clicks but removes their sentence
links; clearing history removes both searches and clicks. Backup/restore is in
Settings. Restore previews counts, requires confirmation, and makes a safety copy.

Personal data lives in `%LOCALAPPDATA%/Kotoba/personal.sqlite`; replaceable
resources live separately under `%LOCALAPPDATA%/Kotoba/reference/`. Set
`KOTOBA_DATA_DIR` for a completely isolated development session. The development
launcher removes that override unless `-DataDirectory` is explicitly supplied.

## Build and check

Requires a .NET 10 SDK and Windows. The machine-wide preview SDK is too old;
use a current SDK or pass its executable explicitly:

```powershell
./scripts/build.ps1 -DotnetPath 'C:/path/to/dotnet.exe' -Publish
./scripts/check.ps1 -DotnetPath 'C:/path/to/dotnet.exe'
./scripts/launch.ps1
```

Build artifacts and NuGet/CLI caches stay under the repository. Build scripts
do not modify PATH or install a system SDK. Core checks need no dictionary;
full-data checks take a separate fixture and optionally the read-only Anki Stats
classification baseline. See [.docs/IMPLEMENTATION.md](.docs/IMPLEMENTATION.md)
for commands, observed performance, verification and known gaps.

Add `-Ui -ReferenceDirectory .tools/test-data` to `scripts/check.ps1` for native
WPF rendering/idle CPU, selection and compact-layout regressions. UI checks
require a prepared isolated `.tools/` data directory with installed resources.

## Source layout

- `src/Kotoba.Core`: text spans, grouping, inflections, grammar, lookup selection.
- `src/Kotoba.Infrastructure`: UniDic adapter, full dictionary importer, resource
  installation, reference SQLite, personal SQLite and backup validation.
- `src/Kotoba.App`: WPF screens, asynchronous operations, selectable ruby text.
- `tests/Kotoba.Checks`: executable behavior checks, parity and timing probes.
- `scripts`: build, self-contained publish, checks and development launch.

No publishing or merge has been performed. Full resource licenses and library
notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `LICENSES/`.
