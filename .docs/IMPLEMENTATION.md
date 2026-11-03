# Implementation checkpoint — 2026-11-03

The user authorized implementation after the design review. Proposed defaults
were adopted without further product questions. The design's original prohibition
on implementation was superseded by that authorization. Work remains uncommitted
on the existing `codex/dictionary-design` branch; no merge, push or publication.

This checkpoint delivers the analysis experiment and a **runnable dictionary
preview**, not completion of all Stage 1/Stage 2 acceptance criteria.

Current documentation is indexed in [README](../README.md), with the
[user's guide](USER-GUIDE.md) for operation and
[program guide](program-guide.md) for internals. The documentation structure follows
the Anki Stats reference project, inspected read-only. This implementation record
retains historical revision details; older names, counts and superseded UI behavior
below should be read in that context. Future work is separated in
[FUTURE-PLANS.md](FUTURE-PLANS.md). Local packaging now includes README and `.docs/`.

## Revision: 2-point zoom, HTML-wrapped kanji and natural ruby

Latest preview feedback, 2026-11-03; supersedes the 10-point zoom and stretched
header ruby described in the historical revision below.

- Ctrl+Plus/Minus now changes the base reading size by **2 pt** (8/3 WPF units).
- Diagnosed the real 分 note read-only: its stored Front is `<div>分</div>` and
  Back is `part`. AnkiConnect `Front:分` returned no IDs, while `Front:*分*`
  located it. Candidate searches now use the latter form and still require exact
  equality after stripping HTML. Multi-character expressions such as 分解 remain
  excluded. No Anki cards or templates were modified.
- Missing exact cards display lowercase **unknown**, retain the kanji and remain
  clickable for a popup. Empty results are not cached, allowing newly added cards
  to appear on the next selection. Network/service errors remain distinct from
  unknown results. A live client probe returned `分: part` and `龘: unknown`, and
  both the known card and the unknown popup were checked in the isolated UI.
- Inspected the user's Japanese note styling/templates read-only. The back uses
  `{{furigana:Front}}` inside a 52px Japanese-font fallback stack, with browser
  ruby layout; its CSS has no glyph-stretching rule. See the
  [Anki ruby filter documentation](https://docs.ankiweb.net/templates/fields.html#ruby-characters).
  The distortion was Kotoba's `Viewbox Stretch.Fill`, which applied unequal
  horizontal/vertical scaling to force the reading into the spelling's box.
  `NaturalRuby` now renders normal-proportion grapheme clusters at half the base
  font size, adjusting space between glyphs. It follows the corresponding
  Japanese font fallback families. Long readings can expand the shared ruby/base
  width; exceptionally long ones reduce font size uniformly, never squeeze one
  axis. The passage's ruby renderer also no longer applies horizontal-only
  scaling. This follows the native-ruby approach; different text engines can
  still produce small metric differences.
- Explain grammar has its own row **below** the passage legend, with no shared
  ruby/header space. The decorative app banner collapses in short windows to
  preserve room for results. A 32-unit gap and thin vertical divider separate
  dictionary spelling from senses, or a horizontal divider in stacked layouts.

Validation: **96 core/infrastructure checks + 28 WPF checks**, all passing.
New assertions cover wildcard candidate discovery plus exact HTML-cleaned
matching, missing/newly-added cards, 2-point zoom, glyph proportions unchanged
when ruby width grows, button/legend vertical positions and the entry divider.
Existing classification parity, selection/copy and idle-render regressions pass.
UI verification uses `.tools/ui-data/`; user history was not used as fixtures.

## Revision: reading size, na-adjectives, entry formatting and clipboard

Implemented after additional preview feedback on 2026-11-03. The old slider
changed only the passage; definitions and input had fixed sizes. The shared
reading-size setting now updates the input, passage, dictionary header/senses,
kanji meanings and grammar cards. Ctrl+Plus/Minus (main keyboard or numpad) changes
the base size by 10 typographic points (40/3 WPF units). The range is 12–72 pt;
Settings shows the current value, uses the same setting and persists changes.
Definitions update without recording clicks or re-querying Anki. Large text
uses stacked form/definition layout when two readable columns cannot fit.

Dictionary header ruby is white like the spelling, smaller, and fitted to the
same width. Spelling and senses are native read-only selectable text controls.
Common is a distinct green badge, JLPT N-levels are blue, beneath the word.
Badge width accounts for zoom so Common word stays on one line. Other Forms
contains only genuinely different compatible spelling/reading pairs; source
restrictions/tags are retained. 巨大 therefore has no duplicate Other Forms row.

Results display concise English POS names (Noun, Na-adjective, etc.). Settings
has a project-authored dictionary terminology guide, including futsuumeishi and
keiyōdōshi; original source label wording remains available on hover. Label codes
are checked against [EDRDG's JMdict help](https://www.edrdg.org/jmwsgi/edhelp.py?svc=jmdict).

Grouping version 2 joins immediately adjacent attributive な after a dictionary-
validated na-adjective, such as 巨大な, 静かな or 有名な, while retaining the root ID
and internal morphology. The definition/copy root remains 巨大. Whitespace and
prohibitive な are negative controls. Historical analysis snapshots retain their
original version; new searches/reanalysis use the updated grouping.

ANKI card copy writes the selected root plus compatible reading to the clipboard,
e.g. **巨大[きょだい]** (the reading is きょだい, not きょうだい). Kana-only or unknown-
reading forms avoid redundant/fabricated reading brackets. This records no
lookup and does not create/change an Anki card. A native test clicked the button
and pasted the exact output into the isolated app input, without submitting it.
Ctrl+Plus and Ctrl+Minus were exercised against an open definition and visibly
changed all reading text. Only isolated `.tools/ui-data/` settings/history were
used. Actual Japanese IME composition and additional display scales remain
manual verification gaps.

Validation for this revision: 93 core/infrastructure assertions and 25 WPF
assertions, including the original parity and idle-render regressions plus
na-adjective grouping/negative controls, root copy formatting, genuine alternate
forms, selectable spelling/senses, badge color/position/zoom, matched header-ruby
width/color, live resize/persistence and no extra clicks while resizing.

## Revision: Enter, results layout, Anki kanji, and UI performance

Requested after the initial preview, implemented 2026-11-03. This section
supersedes the initial checkpoint's shortcut/layout/UI-performance notes below.

- Enter submits from the input; Shift+Enter inserts a newline. Ctrl+Enter remains
  a compatible alias. IME processed/composition-confirming Enter is guarded.
- Search is pinned above a bounded passage. Words/alternatives occupy the left
  pane; kanji cards and sentence grammar occupy the right. Definitions place
  the form/reading beside senses. Long text scrolls within a pane, keeping the
  input and passage visible. Short windows reduce the input/passage height.
- Lexical units have underlines, POS colors, and display-only gaps. Original
  source spans/text are retained; selection copy/lookup removes only added gaps,
  preserving intentional whitespace and paragraph breaks. Word selection updates
  existing runs instead of replacing the FlowDocument and ruby layer.
- Kanji cards/popups read exact single-kanji notes from local AnkiConnect,
  configured with deck 漢字, expression field Front, meaning field Back by default.
  Deck and field names are editable. Only read actions getActiveProfile,
  findNotes and notesInfo are permitted. Queries are fixed to loopback; proxy
  use and HTTP redirects are disabled. No collection file access or note writes.
  Results are cached in memory (256 keys), scoped by active profile and mapping;
  refresh clears the cache. Missing service/errors never block definitions.
  Kanji card openings do not create word lookup events.

**Performance root cause:** the first ruby adorner subscribed to LayoutUpdated
and called InvalidateVisual unconditionally. InvalidateVisual itself invalidates
arrangement/layout, causing a self-sustaining layout/redraw loop after the first
query. The earlier 0.15 ms timing covered Core analysis only and missed this
native UI failure. [Microsoft's InvalidateVisual documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.invalidatevisual?view=windowsdesktop-10.0)
describes its layout invalidation behavior.

The replacement adorner has **no LayoutUpdated subscription**. It builds frozen
cached drawing geometry on initial content, actual size changes and passage
scrolls, coalesces pending rebuild requests, paints the cached drawing, and
explicitly unsubscribes when replaced/closed. OnRender performs no text geometry
queries or layout invalidation. Definition selection reuses the document/layer.

Measured on the same machine with the supplied sentence in an isolated test app:

| Five-second post-search idle sample | CPU time | Working set |
| --- | ---: | ---: |
| Original preview | 5,031 ms / 5,028 ms elapsed | 167.3 MiB |
| Fixed build, definition and live kanji visible | 15.6 ms / 5,024 ms elapsed | 162.5 MiB |

This removes continuous single-core saturation in the reproduced case. These are
short local samples, not universal latency guarantees or a hardware benchmark.

Verification: **83 core/infrastructure checks** pass, including 11 new Anki
transport/matching/cache/cancellation checks and the original full-set parity
checks. **13 native WPF checks** cover initial ruby rendering, no repeated idle
render/rebuilds, an idle CPU regression threshold, document reuse on selection,
display spacing/source copy, ruby alignment, multiline/intentional whitespace,
bounded paragraphs, scroll refresh/return to idle, and an 800×620 compact layout.
Small startup CPU samples vary with JIT; render/rebuild counters remain steady.

Native computer-use inspection exercised plain Enter, colored/underlined spaced
words, side-by-side definition/kanji results and a live 床 popup. Anki's actual
exact card returned its stored keyword **bed**; the left JMdict definition for
the context-supported ゆか entry correctly remained **floor**. They are different
sources and are labeled separately. Anki was read only; no cards were changed.
All UI/history writes used `.tools/ui-data/`, not the user's personal Kotoba DB.

Reproduce native checks with `scripts/check.ps1 -Ui -ReferenceDirectory
.tools/test-data` and a .NET 10 SDK path. Real IME composition with a Japanese
keyboard and other display scale factors remain manual coverage gaps.

## Implemented

- C#/.NET 10, native WPF, dark Search/Personal dictionary/Statistics & history/
  Settings navigation, resizable window, Japanese text size setting.
- Explicit search and Ctrl+Enter; Ctrl+L; Unicode scalar guard above 5,000;
  asynchronous analysis, cancellation/stale-result checks. Failed/canceled
  searches retain the previous successful passage and do not save a search.
- NMeCab 0.10.2 with the UniDic 2.1 adapter and UniDic-lite 1.0.8 binary data.
  Original text/UTF-16 offsets retained, including surrogate pairs, punctuation,
  tabs, spaces, CRLF and blank lines. Native runtime requires no Python or Java.
- Dictionary/reading-supported lexical grouping, literal personal overrides,
  root validation and morphology-backed conjugation chains. Internal morphology
  remains attached to grouped units.
- Real selectable document text with ruby painted in a local adorner layer;
  kana-anchor alignment where unambiguous and whole-word fallback. Selecting a
  grammar card colors its actual span offsets, including inside grouped units.
- Full English JMdict SQLite index: all senses kept separately, spelling/reading
  restrictions, tags/parts of speech, form metadata, common and JLPT identities.
  Alternatives require an explicit Open action to count.
- Stable personal entry IDs, duplicate-expression update, add/edit/delete,
  immediate reanalysis without extra searches/clicks; selection lookup/add actions.
- Search/analysis snapshots and independent click events in personal SQLite;
  dedicated five-click/date-filtered statistics, daily totals, click surfaces/
  contexts, paged submitted history, reopening and deletion controls.
- Transactional initial schema, identity/time indexes, consistent SQLite backup,
  archive/schema/span validation, confirmation preview and pre-restore backup.
  Reference stores remain separate from personal data.
- Explicit pinned resource installation with streaming progress/cancel, SHA-256
  verification, native loading probe, staged indexing and atomic active-version
  pointer. Canceled installs retain the previous active resources. Manual checks
  verify availability of the app's supported pin; they never advance Common/JLPT
  datasets independently of Anki Stats.
- Self-contained Windows x64 app folder at `artifacts/Kotoba/`, build/check/launch
  scripts and bundled third-party notices/licenses.

## Stage 0 findings

UniDic-lite packages UniDic 2.1.2 (2013) with the upstream lite modifications.
NMeCab's UniDic 2.1 adapter successfully loaded and analyzed its binary format on
this machine. Its original columns provide surface spelling, base spelling,
reading and conjugation features. Using the orthographic base preserves 湧く
instead of replacing it with the broader lemma 沸く. Dictionary-validated godan
potential relationships recover roots such as 使う, 読む and 書く.

Raw morphology splits 自分勝手, 一緒に, 嫌悪感 and 焼け野原; dictionary/reading evidence
now groups them. Verb continuative + noun compounds require an explicit noun entry.
The supplied passive example groups 吹き飛ばされ and recovers 吹き飛ばす with passive
and continuative layers. 落ちた recovers 落ちる. Causative/passive/negative/past,
potential/negative, progressive and polite/past probes pass.

Raw morphology misreads the rare sound expression しゃらん as auxiliary fragments.
That lexical problem is **still open**: the current build preserves the text and
supports manual selection/personal entries, but does not claim correct automatic
sound-symbolic segmentation. The surrounding という + noun construction is detected.
Literary names and ambiguous readings also need broader evaluation.

No reason to switch to a Sudachi worker was established by these initial probes.
The replaceable `IMorphology` interface remains available if broader evaluation
shows that a change is warranted. This is not a comparative analyzer benchmark.

## Observed validation

The executable suite passed **72 checks, zero failures** against the full dictionary,
plus a separate successful end-to-end resource installation/recovery check.

Full-set comparison against Anki Stats' local pinned baseline:

| Set | Exact matching entry IDs |
| --- | ---: |
| Common | 22,649 |
| N1 | 3,056 |
| N2 | 1,760 |
| N3 | 1,653 |
| N4 | 630 |
| N5 | 669 |

JMdict import: **218,963 entries**. **11 unresolved JLPT rows** remain unlinked.
Anki Stats source/reference files were read only; no personal Anki data was read.

Checks cover scalar guards, custom overlaps/shorter inputs/names, original offsets,
cancellation, stable click identities, A → B → A/reclick behavior, threshold within
dates, Eastern-time 23/25-hour DST days, snapshot round-trip, deletion/unlinking,
backup/restore, invalid backup rejection, retained settings/entries, lexical
compounds, inflection chains, grammar controls, full senses and classification sets.

Grammar fixtures: **9 positive assertions and 6 negative/scope controls**, all
passing. This is a small authored regression set, **not a general accuracy or
coverage estimate**. It includes related new sentences in addition to supplied
examples. A held-out VN corpus and independent linguistic review remain required
for the useful-grammar milestone.

Actual production-URL installation was exercised into `.tools/install-data/`:
downloaded all six classification resources plus UniDic, verified checksums,
built the index, opened dictionary/morphology, then canceled a reinstall and
confirmed the active pointer was unchanged. No personal text was sent.

Indicative warm **core analysis** timings on this machine, ten iterations after
warmup: 41-character supplied sentence ~**0.15 ms**; repeated 410-character
paragraph ~**1.2 ms**. These exclude cold loading, UI layout/rendering, setup and
grammar, and are not end-to-end latency guarantees. Reference SQLite plus extracted
UniDic occupied **399.8 MiB**; installation retains additional source archives.
Startup/working-set profiling and cold/end-to-end UI timings are still pending.

Native inspection used `.tools/ui-data/`, never the normal personal directory.
Verified dark main layout, passage/ruby, word clicks, passive definition, grammar
generation, selectable text, scrolling alignment, personal entry saving/reanalysis,
statistics/history showing deliberate clicks, and Settings resource versions.
Ctrl+Enter was exercised. Fixed light outer-window background, intercepted clicks,
and ruby movement during outer scrolling. Other display scales, real Japanese IME
composition, complete keyboard navigation and large-definition/large-passage
visual testing remain pending. The personal editor now scrolls at shorter heights.

## Current grammar coverage and remaining work

Project-authored offline rules/cards cover しか～ない, という + noun,
っていうのに / というのに, のでもなく / のではなく, なりに, ～ば
(including 気が付けば) and ～まい. Rules link local negative predicates and stop
at punctuation/quotation/clause evidence conservatively. Some valid longer-distance
relations are intentionally missed. Several recognizers still rely on short text
patterns and must gain stronger token/clause constraints before broad coverage
claims. Context-sensitive interpretations are marked as uncertain.

Before calling Stage 1 complete: broader lexical/reading/restriction probes;
automatic sound-symbolic handling; saved-entry timestamp/migration expansion;
interruption/tampering/migration fault injection; very large input UI checks;
real IME, accessibility, keyboard and 125/150% scaling verification. Audit
ambiguous compound ranking and kana-only expressions against unrelated contexts.

Before Stage 2: expand and independently review the grammar catalog, measure a
held-out corpus/negative controls, improve clause scope/contractions/overlaps,
and disclose precision/coverage gaps. Ollama is not implemented or selected;
dictionary/built-in operation requires no model. A signed/coordinated remote
resource manifest and pruning old immutable reinstall directories also remain
future resource-management work; this build checks/reinstalls its embedded pin.

## Reproducing checks

### Personal categories and rich definitions (2026-11-03)

Added Personal words and Names categories, All/category list filters, and editor
category controls. New entries inherit the active category filter; existing entries
default to Personal words without guessing which are names. Both categories remain
literal overrides with the same identity, precedence and lookup counting behavior.
Changing filters does not save or count a lookup.

English definitions now use a native RichTextBox with bold/italic commands,
numbered/bulleted lists, subscript and Small note buttons. The latter produces
smaller baseline text like Jisho's inline usage labels; subscript uses WPF's native
Typography.Variants representation. BaselineAlignment alone did not survive WPF
RTF serialization and was replaced after the persistence check caught it. Definitions
retain plain text for lexical/history snapshots and base64-encoded RTF for editing
and formatted, selectable search results. Only RTF is loaded, never arbitrary XAML.
Malformed/missing formatting falls back to plain text; formatting is capped at
400,000 encoded characters. Personal definition text retains its 20,000-character
limit. No network is involved, and no grammar/morphology coverage changed.

SQLite schema 2 adds category and definition_rtf columns. Existing schema-1
databases are copied to a unique `before-schema-2-*.sqlite` file before a transactional
migration; entries/settings/history keep their values and IDs. Backups retain their
manifest format and support schema 1/2 databases; old restores are upgraded with a
safety snapshot. Old executables reject schema 2 rather than silently losing data.

Validation: 106 core/infrastructure and 40 WPF checks pass (146 total), including
schema migration/snapshot, categories, formatted-field persistence, old/new backup
restore, bold/italic, subscript/list round trips, category filtering and formatted
personal search display. Common/JLPT full-set parity remains exact, read-only.
All fixtures are isolated under `.tools/`; no personal database was used for testing.

Removed obsolete `artifacts/Kotoba` and `artifacts/Kotoba-staging` packages after
checking resolved paths were direct non-reparse children of this workspace's
artifacts directory and contained no personal SQLite. `%LOCALAPPDATA%/Kotoba`
remains the live compatible data directory and is not obsolete; it was not deleted.

### Gengo naming and personal workspace revision (2026-11-03)

The product now displays Gengo / 言語 / げんご, and publishes to
`artifacts/Gengo/Gengo.exe`. Window/dialog titles, executable metadata, manifest,
backup labels and launch/build scripts use the new name. Code namespaces/project
names, personal SQLite schema, backup format, `%LOCALAPPDATA%/Kotoba` and
`KOTOBA_DATA_DIR` stay compatible, avoiding a data-directory migration or duplicate
resource installation. Earlier `artifacts/Kotoba` packages are older builds.

The personal list's `MaxHeight=550` caused WPF to center it within the available
tall-window space. Replaced the surrounding outer scroll/DockPanel arrangement
with a bounded two-column workspace: list stretches below its heading, editor
scrolls independently, and a capped 440-unit list column leaves room for the form.
Rows show only expression; saved readings remain editable and used in analysis.
No personal records are transformed or removed by this presentation change.

Validation: 101 core/infrastructure and 33 WPF checks passed. The new layout
checks cover 1710×1400 and 800×620 windows, require the first list item within
100 units of the workspace top and editor aligned at the top, check compact
editor scrolling, and verify expression-only rows retain readings in the editor.
Only isolated `.tools/ui-data` fixtures were used; Common/JLPT parity stayed exact.

### Kanji detail revision (2026-11-03)

Kanji card clicks now open independent offline KANJIDIC2 details alongside the
live-bound Anki keyword/source. Unknown Anki cards and Anki service failures do
not suppress character definitions. The popup shows every English meaning and
Japanese On/Kun reading in its source group, optional reading qualifiers, nanori,
stroke counts and school grade. Missing readings/English meanings and a missing
dictionary entry are distinct states. Dots/hyphens retain upstream notation.
The popup has a bounded scroll region and a persistent Close button.

The unchanged `kanjidic2-all` ZIP is bundled as an assembly resource: pin
`3.6.2+20261026175559`, file version 4, date 2026-10-26, database 2026-299;
13,108 characters, including records without English meanings. SHA-256 is
`4C5969C69A5A3B21F87254A440D3BBC5E92420793BB22D78E4CCAA603AEC2501`,
verified against official GitHub release asset metadata and again before reading
at runtime. Exact URL and license are in `THIRD-PARTY-NOTICES.md` and code.
Loading/parsing runs once on a worker thread; subsequent clicks use an immutable
character index. No network, personal SQLite, Anki writes, or dictionary reinstall
is needed for these details. The full upstream archive remains unchanged; runtime
presentation selects Japanese readings/English meanings, not other languages or
dictionary-reference indexes. Legacy kanji JLPT numbers are deliberately omitted
because they are not modern N-levels. Common/JLPT word membership pins are unchanged.

Explain grammar still uses project-authored `builtin-1` rules on the entire
existing morphological analysis: no LLM, network, translation, or grammar lookup
in JMdict. The displayed sample sentence matches none of the supported rules;
notably であり, のように, passive clause linkage and 事だけ are not sentence-grammar
rules yet. Word-level passive/past analysis is separate. The empty-state wording
now explicitly identifies a limited-rule result rather than a processing error.
No new grammar coverage is claimed by this revision.

Validation: 101 core/infrastructure checks and 30 WPF checks passed, including
full-set read-only Common/JLPT parity with Anki Stats. Added 地 On meanings,
面 Kun/nanori/grade, 分 okurigana notation, readings without English meanings
(乁), unknown Anki popup and missing dictionary entry checks. WPF search idle
used 93.75 ms CPU over 700 ms, with zero ruby rebuilds/renders. First cold kanji
popup took 535 ms including background archive verification/parsing and layout.
The idle regression sample runs before cold popup work so one-time parsing/GC
is not confused with a continuous rendering loop.

Use a .NET 10 SDK. In this environment the existing read-only SDK executable was
`D:/Drake/git-projects/anki-stats/.tools/dotnet/dotnet.exe`; the machine-wide
`dotnet` command selects an obsolete preview SDK. All build output/NuGet/CLI state
for this project stays in this repository. The SDK is a build prerequisite only;
the published application includes its runtime.

```powershell
$sdk = 'C:/path/to/dotnet.exe'
./scripts/build.ps1 -DotnetPath $sdk -Publish
./scripts/check.ps1 -DotnetPath $sdk
# Optional: real pinned installer, completely separate data directory.
& $sdk tests/Kotoba.Checks/bin/Debug/net10.0/Kotoba.Checks.dll --install .tools/test-data
# Full-data behavior/parity checks (baseline is optional, read only).
./scripts/check.ps1 -DotnetPath $sdk -ReferenceDirectory .tools/test-data -ParityBaseline 'C:/path/to/coverage-reference-v1.json.gz'
./scripts/launch.ps1 -DataDirectory .tools/test-data
```

The Windows sandbox blocked atomic replacement of the temporary restore database;
the same checks passed outside the sandbox using isolated generated data. No
personal database was used for the test. Native launch similarly required the
environment's normal GUI-launch approval.

Dependency/resource provenance: [NMeCab](https://github.com/komutan/NMeCab),
[UniDic-lite](https://github.com/polm/unidic-lite),
[jmdict-simplified](https://github.com/scriptin/jmdict-simplified),
[OpenJLPT](https://github.com/evanclan/OpenJLPT). Library/data license texts and
exact pins accompany the app in `THIRD-PARTY-NOTICES.md` and `LICENSES/`.
