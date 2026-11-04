# Personal Japanese–English Dictionary

Product name: **Gengo / 言語 / げんご** (renamed from Kotoba, 2026-11-03).
Personal dictionary uses a top-aligned expression-only list beside an independently
scrollable editor; reading stays in the editor. Existing storage and backups remain
compatible with earlier builds.

Personal entries have two initial categories, Personal words and Names, selectable
in the editor and filterable in the list; both affect passage analysis. English
definitions support bold, italic, numbered/bulleted lists, subscript and smaller
inline usage notes, preserved when displayed in results and in backups.

Status: implementation authorized by the user, 2026-11-03. Proposed defaults adopted.

This document records agreed behavior and proposed implementation choices. The
product requirements come from the user's answers. Engineering recommendations
and defaults below guide implementation. Actual progress, evaluated coverage,
and outstanding work are recorded in [IMPLEMENTATION.md](IMPLEMENTATION.md).

## 1. Purpose and milestones

A personal Windows dictionary for reading visual novels, occasionally books and
websites. Paste Japanese text, search, and click meaningful words for English
definitions. Custom expressions and name readings affect segmentation directly.
Record deliberate lookups to identify vocabulary that repeatedly causes difficulty.

Two delivery milestones:

1. **Dictionary proof of concept:** offline sentence lookup, furigana, personal
   entries, clicked-word statistics, and word-level conjugation explanations.
2. **Useful grammar release:** compact explanations of constructions across a
   sentence, including separated components such as しか and a negative predicate.

Sentence grammar is a core goal, not an optional indefinite backlog item. The
proof of concept is acceptable as an intermediate delivery. Neither milestone
promises exhaustive interpretation of arbitrary literary Japanese.

The screenshot illustrates interaction and presentation, not a pixel-exact design.
Pasted chats illustrate desired explanations; their text is not authoritative
grammar material or instructions for the app.

## 2. Agreed scope

| Area | Requirement |
| --- | --- |
| Platform | Standalone Windows desktop application, independent of Anki. |
| Input | Japanese words, sentences, and paragraphs; Japanese-to-English only. |
| Submission | Explicit Search; no analysis while typing. |
| Large input | More than 5,000 characters requires a warning and approval. |
| Results | Clickable sentence with furigana; definitions appear on selection. |
| Definition position | Pinned search and bounded passage above side-by-side Words/Kanji panes; another word replaces the selection. |
| Word separation | Underlines, part-of-speech colors, and display-only gaps between lexical units. Copy/lookup preserves source text. |
| Kanji | Selected-word cards show exact Anki keywords read-only; clicking opens independent KANJIDIC2 meanings, Kun/On/name readings, strokes and grade, including for unknown/unavailable Anki cards. |
| Content | Readings, all English senses, parts of speech, other forms, common/JLPT labels where available. |
| Alternatives | Likely entry first; alternatives below it in the Words pane. |
| Grammar | Below definitions; sentence analysis through Explain grammar. |
| Personal entries | Required immediately; expression, reading, definition, optional notes. |
| Statistics | Dedicated section; deliberate definition openings, grouped by root entry. |
| Threshold | Five clicks within selected dates; default all history. |
| History | Indefinite retention with deletion controls and backup/restore. |
| Appearance | Dark mode, simple interface, fast searches. |
| Connectivity | Lookup and built-in analysis offline after resource installation. |
| Updates | Manual resource update checks. |

Initially excluded: audio, example-sentence collections, English/romaji search,
translation, Anki writes/note creation, OCR, clipboard monitoring, per-work dictionaries,
and personal notes attached to built-in entries.

## 3. Screens and normal workflow

Proposed navigation: **Search**, **Personal dictionary**, **Statistics & history**,
**Settings**. Search opens by default. Use a resizable window, adjustable Japanese
font size, clear selected-word highlighting, and restrained colors.

```text
Search | Personal dictionary | Statistics & history | Settings

[ Japanese text input                                ] [Search]

Analyzed sentence with furigana and clickable units
                                            [Explain grammar]

Preferred form + reading       Parts of speech / labels
                               Numbered English definitions
                               Other Forms

Word conjugation explanation
Sentence grammar cards, after Explain grammar

Other dictionary candidates
```

Search initially shows only the analyzed text, not a definition list. Proposed
single-word behavior is the same: click to open. New successful searches clear
old word selection and grammar results. Failed/canceled searches preserve the
last successful result.

Updated shortcuts: Enter searches from the input; Shift+Enter inserts a newline;
Ctrl+L focuses/selects input. Ctrl+Enter remains a compatible secondary shortcut.
Retain normal text selection, copy, keyboard focus, and Japanese IME
behavior. Enter confirming IME composition must not submit a search.

Revision requested after the first runnable preview: the search input remains
visible while results update. The passage has bounded height; Words and Kanji
use separate columns, with long contents scrolling inside their own panes.
Definitions use a word/reading column beside their senses. Grammar is in the
right-hand details pane. Added spaces are presentation, not changes to source
offsets, saved text, custom matching, or selection lookup/copy.

Reading-area revision (2026-11-04): input and passage grow naturally before
scrolling, with doubled height limits (normally 184/310 WPF units), shared
responsively on short windows to retain usable definition panes. Ruby uses the
same content viewport as the passage: do not draw readings for clipped base text
or reuse ruby geometry from a previous scroll offset.

Kanji cards/popups initially contain the character and meanings from exact
single-character cards in Anki. Default mapping discovered on the user's machine:
deck 漢字 (including children), Front = character, Back = meaning. Settings exposes
this mapping. Read through the loopback AnkiConnect API, without modifying cards
or opening the collection file. Require Anki/AnkiConnect only for this feature;
unavailability never blocks dictionary use. Do not substitute JMdict glosses for
Anki keywords or derive a character's meaning from a containing vocabulary word.

Proposed character convention: count Unicode scalar values, including whitespace
and punctuation. At 5,000 proceed normally; at 5,001 show the count and
**Analyze anyway / Cancel** before analysis or history creation. Approval applies
only to that submitted text. Long searches remain cancellable and keep UI responsive.

Preserve original text, spelling, punctuation, paragraph breaks, and character
positions. Put ruby above recognized kanji portions where reliable, with whole-word
ruby as a fallback for difficult names. Kana-only words need no duplicate reading.
Use context-supported readings rather than blindly choosing the commonest reading.
Custom name readings override automatic readings.

Unknown text stays visible and selectable, with **No dictionary match** and an
add-entry action. Do not invent furigana or silently discard characters. Ambiguous
readings/matches should offer alternatives.

## 4. Definition presentation

Preferred/common compatible form and reading appear left of English definitions,
with furigana above kanji. Proposed preference order: source common/usually-kana
metadata, compatibility with the selected spelling/reading, stable source order.
This is a dictionary preference, not a measured corpus-frequency claim.

Preserve all English senses separately, including parts of speech, usage labels,
and spelling/reading restrictions. **Other Forms** beneath each definition list
contains alternative compatible readings and spellings. Do not flatten all senses
into one gloss field.

Show common/JLPT badges only where supported. An absent JLPT label does not mean
advanced vocabulary. Explain in About/Settings that JLPT lists are community
estimates; avoid repeating that explanation in ordinary results.

**Required parity with Anki Stats:** the Common and N5–N1 sets must mirror Anki
Stats, using the same source snapshots, dictionary identities, and membership
rules, not merely the same providers. The inspected baseline is:

- English jmdict-simplified version `3.6.2+20261026175559`.
- OpenJLPT revision `0d1d3410bec90bd4098a7c72de820543cb4f707c`.
- Common membership: a JMdict entry is common if any of its written forms or
  readings has the source's `common` flag, matching Anki Stats' importer.
- JLPT membership: use each source row's explicit `jmdict_id` and `level`;
  retain distinct memberships and do not guess links for unresolved rows.

Verify these pins and rules against Anki Stats again at implementation time.
Resource updates must preserve this parity: do not independently advance these
classification datasets to a newer upstream snapshot. Coordinate any future
baseline change across both apps and expose installed versions in Settings.
The apps retain independent storage; parity does not require Anki Stats to run.

Selecting another sentence unit replaces the current dictionary selection.
Alternatives appear further down the same results area. Proposed counting
interaction: alternatives have a preview and an explicit open/select action.
Scrolling/rendering an alternative is not a lookup; deliberately opening it is.

Lookup completeness revision (2026-11-04): resolve the analyzer's contextual
candidates together with every exact dictionary entry for the clicked spelling
and explicit inflected root. Context ranks candidates but must not hide other
homographs/readings. Show a compact entry chooser above the selected definitions,
with full candidate previews below. Preserve source senses, reading restrictions
and personal-entry precedence. This also applies when opening saved analyses.

Word conjugation explanations follow dictionary definitions. Sentence grammar
cards remain associated with the submitted text even when word selection changes.

## 5. Personal dictionary

Confirmed fields: expression, reading, English definition, optional notes.
No general notes on built-in entries. Proposed validation: expression and
definition required, reading optional unless custom furigana is wanted. Each
entry receives a stable identity. Exact duplicate expressions are edited rather
than silently creating conflicting overrides.

Add/edit/delete on a dedicated page. Selecting any span in the analyzed sentence
also offers **Look up selection** and **Add personal entry**. Saving reanalyzes
the current text without creating a search event or lookup. Delete confirmation
explains that ordinary matching will resume.

Entries apply globally. Custom definitions appear first; built-in matches remain
available underneath. Custom matching affects segmentation, not merely ranking.

Rules:

- Match literal strings actually present in the input.
- At the same starting position, the longest custom match wins; scan left to right.
- 聖杯戦争 becomes one unit, without clickable 聖杯/戦争 inside.
- Saving both 聖杯 and 聖杯戦争 never expands a shorter 聖杯 input to the longer word.
- 衛宮士郎 with reading えみやしろう displays that ruby on one name unit.
- Custom spans take priority over ordinary dictionary segmentation.

Initial overrides are literal, not fuzzy and not automatically conjugated.
Preserve spaces if intentionally entered. A global literal override cannot always
infer whether the same characters have another contextual meaning; editing/removing
the override is the initial correction path. Context-specific exceptions can follow.

## 6. Segmentation and conjugation

Choose the smallest meaningful lexical unit, not every analyzer fragment. Following
the user's preview feedback, validated na-adjectives join attributive な: 巨大な is
one clickable unit opening root 巨大, with な retained in internal morphology.
Self-standing/prohibitive な is not merged this way. 自分勝手 should retain its compound meaning. 一緒に has no separate
clickable に, but standalone の and を outside recognized expressions remain clickable.

Proposed pipeline:

1. Preserve original text/offsets and locate exact custom spans.
2. Analyze morphology in context: surface forms, readings, lemmas, parts of speech,
   and inflection features.
3. Generate dictionary-backed lexical spans and candidate inflection chains.
4. Rank candidates using compatible readings, grammar, lexical evidence, and common
   flags; choose nonoverlapping display units honoring custom spans.
5. Return ruby, entry identities, original spans, and internal morphology.

Do not always choose the longest built-in substring: ordinary compound recognition
requires linguistic evidence. Keep internal tokens beneath grouped units so UI
grouping does not erase information needed for sentence grammar. Grammar
highlights use original offsets, including within combined display units.

Below a selected definition, show the original inflected form, dictionary root,
ordered endings, and concise explanation. Support potential, passive, causative,
negative, tense, politeness, conditionals, and common auxiliary chains as explicitly
tested. Preserve genuine ambiguity. Dictionary validation should prevent arbitrary
suffix stripping from inventing a root.

Acceptance example:

> それが巨大な鉄槌であり、自分がゴミのように吹き飛ばされ、地面に落ちた事だけ、判る。

吹き飛ばされ must lead to 吹き飛ばす, with a passive connective analysis in this
context. Do not label it causative merely because the initial request called it
that. 落ちた leads to 落ちる. Chained endings can be explained internally without
making each ending a separate clickable vocabulary item.

## 7. Explain grammar

### Built-in engine: preferred default

**Explain grammar** processes the submitted text and displays compact cards below
definitions. Proposed format: **pattern — meaning**, relevant text, brief component
breakdown, and one or two sentences explaining the combination. Selecting a card
highlights its matching spans. Supply paragraph context when it was submitted;
do not manufacture missing context.

Example:

> **しか～ない — nothing but / only**
> 嫌悪感 = disgust/aversion; 湧かない = negative form of 湧く.
> Together, しか and the negative predicate exclude everything except the named
> thing: only disgust arises.

Use a versioned offline catalog of reviewed explanations plus explicit recognition
rules over tokens, inflections, and clause scope. Formation descriptions in a
dataset are reference content, not an executable parser.

Recognizers must link separated components, handle contractions, and avoid pairing
しか with an unrelated negative in another sentence/clause. Rank overlapping
patterns, avoid duplicate cards, and indicate ambiguous interpretations.
**No supported pattern identified** means limited coverage, not that the sentence
contains no grammatical construction.

OpenJLPT is a candidate content source, supplemented by project-authored records.
Its grammar collection does not establish automatic sentence-recognition coverage.
Measure coverage and false positives on unseen sentences and negative controls.

### Acceptance examples

Targets below require review during implementation; do not copy pasted answers
uncritically or satisfy the cases with hardcoded full-sentence matching.

| Input | Intended behavior |
| --- | --- |
| 嫌悪感しか湧かない | Link しか and negative predicate; explain only/nothing but. |
| どうせ君に令呪は使えまい。 | Recover 使う and potential/negative-conjecture layers; retain contextual alternatives for まい. |
| だっていうのに、 with prior context | Recognize contrastive fragment “and yet/despite that,” rather than unrelated standalone だって. |
| バカにするのでもなく、真剣に綾子は頷く。 | Recognize バカにする and nominalized negative connective characterization; avoid inventing an unstated object. |
| しゃらん、という華麗な音。 | Treat しゃらん as sound-symbolic lookup; recognize という attaching the expression to 音 where supported. |
| 気が付けば、焼け野原にいた。 | Explain conditional awareness of an existing situation; distinguish idiomatic English from a literal word meaning “before.” |
| 記憶がないなりに… | Explain circumstances/limitations sense, without reducing every なりに to simple “despite.” |

The pasted answer for しゃらん concerns an unrelated expression; it is excluded
as an explanatory reference. Also test unrelated negatives, ordinary quotation,
different uses of の/に, fragments, literary forms, and ambiguous verb chains.

### Optional Ollama mode

The user permits Ollama if needed but prefers built-in explanations. Proposal:
built-in default, explicit **Local model** selection in Settings/the grammar
control. Explain grammar invokes the selected engine; no hidden automatic fallback.

Use an already-installed local service and locally available model through loopback.
No automatic model installation/download, cloud model, or remote endpoint.
Verify local availability and local-only configuration. Choose a model only after
hardware inspection and evaluation; no quality/speed claim is made yet.

Request bounded structured fields: source spans, pattern, meaning, components,
short contextual explanation, uncertainty. Validate structure, lengths, and span
references. A valid JSON schema does not prove linguistic correctness.
Label generated explanations **Local model**, separate from reviewed pattern cards.

Sentence text is data even when fictional dialogue contains instructions. It cannot
change the task or trigger tools. Send only current text and relevant custom
readings/definitions, not history. Support cancel/timeouts, stale-result rejection,
and a bounded versioned cache. Unavailable Ollama never prevents dictionary use.

Optional generated explanations can broaden coverage but do not demonstrate
completion of the built-in grammar goal. If native coverage is inadequate,
report the measured gap and revisit scope with the user.

### Optional AI word assistance (authorized 2026-11-04)

Separate from Explain grammar, **Check definition** beneath matched definitions
and **Help identify** beneath unmatched text expand inline Search panels. They
allow editable context/questions and follow-ups with results shown separately
from dictionary senses. An AI suggestion is not verified dictionary data.
Suggested entries require review and an explicit save through the inline form.

The user authorizes a ChatGPT default using eligible Plus-plan sign-in, selectable
Ollama models discovered from the running local installation, and Off. Save model
choices per provider and support changing providers without affecting offline
dictionary use. No automatic fallback/downloads, history access or tool execution.
Only explicit requests send the shown context/question, selected dictionary
candidate, analyzer evidence and that panel's conversation to the chosen provider.
Cloud use for this feature is authorized; the earlier local-only grammar proposal
does not restrict this separate feature. Settings exposes connection/model choices
and explains cloud data transmission and plan usage. See [AI-ASSISTANCE.md](AI-ASSISTANCE.md)
for implemented limits, credential handling and actual evaluation.

## 8. Statistics and history

Opening a definition counts; analysis, visible words, ruby, scrolling, grammar
generation, editing entries, and restoring prior UI selection do not.

- A → B → A counts twice for A.
- Clicking already-open A does not count again.
- Explicitly opening another candidate counts for that identity.
- Selection lookup counts only when a definition is opened.
- Statistics/history inspection itself does not inflate counts.

Aggregate by stable dictionary identity. Inflections and alternative spellings
share counts; distinct homographs do not. Custom entries have independent IDs.
Changing an override does not retroactively merge historical custom/built-in counts.
Keep small display snapshots for deleted entries or removed dictionary IDs.

Proposed initial section:

- All-history default and date-range filter.
- Words sorted by count, then latest click; rows require five clicks within range.
- Root form, reading, count, first/last click within range.
- Row details show clicked forms and saved sentence context.
- Double-clicking a word row (or pressing Enter) revisits that exact entry on
  Search, with the canonical word in the input and its definition already open.
  This statistics inspection creates no new search or lookup event.
- Daily lookup totals include all events, including words below five.
- Chronological submitted-text history with reopen/delete controls.

Keep submitted searches separate from clicked-definition events. Store clicked
surface form and optional sentence link. Reopening history creates no duplicate
search event; subsequent deliberate definition openings count normally.

Both exact submission history and aggregate detected-word statistics are desired.
The latter is deferred. Save versioned analysis snapshots so future reporting
does not pretend newer segmentation reproduces original analysis.

Indefinite retention is reasonable for expected personal text volume. Avoid
duplicating whole dictionaries or definitions per event. Page history results,
expose database size, and avoid unbounded in-memory loading.

Proposed deletion semantics: deleting a submitted search removes text/analysis,
preserves click counts, and removes their sentence links. Clearing all history
removes both searches and clicks. Provide selected/date-range/all-history controls
with confirmation explaining effects. Personal entries/settings remain.
Older backup files remain independent copies.

## 9. Data, resources, and backup

Use separate reference and personal SQLite stores under per-user local application
data. Reference data is rebuildable; personal data must survive resource updates.

| Logical records | Contents |
| --- | --- |
| Resource versions | Source/version, hash, schema, notices. |
| Entries/forms/senses | Stable IDs, compatible forms/readings, glosses/tags. |
| JLPT membership | Conservative entry-ID links and source version. |
| Grammar catalog/rules | Explanations and recognition versions. |
| Personal entries | Stable ID, expression, reading, definition, notes, timestamps. |
| Searches/analysis | Text, UTC time, offsets, resource/analyzer versions. |
| Click events | UTC time, identity, surface/root snapshot, optional search link. |
| Settings | Appearance, font size, selected grammar mode/local configuration. |

Use transactions, schema migrations, pre-migration backups, and identity/time
indexes. Store UTC; display/filter local Windows calendar dates with correct
daylight-saving boundaries.

Initial setup proposal: explicit **Install offline resources**, showing size,
progress/cancel. Once installed, lookup and built-in analysis need no internet.
First launch without resources cannot perform dictionary lookup offline; say so
clearly. Bundle local notices with the app. A full offline installation bundle
can follow if needed.

Manual resource checks use a supported version manifest. Stage downloads, verify
trusted pinned checksums/schema, build indexes, then switch atomically. Failed or
interrupted setup/update preserves the last good version. Never transmit personal
text in resource requests. Never silently rewrite historical analyses.

App updates are manual packages initially, separate from dictionary/resource
updates. Resource notices remain readable offline.

Backup proposal: a versioned archive with a consistent personal database snapshot
and resource manifest; omit replaceable dictionary data and model weights.
Restore validates versions, previews contents, makes a pre-restore backup, and
replaces personal data only after explicit confirmation. No merge initially.
Explain that restoring on another machine may require resource/model installation;
a personal backup is not a complete offline installer.

## 10. Recommended technical approach

Engineering proposals, not claims of tested compatibility:

| Component | Recommendation |
| --- | --- |
| Windows app | C#/.NET 10 and WPF, consistent with Anki Stats. |
| Persistence | SQLite through Microsoft.Data.Sqlite. |
| Definitions | Full English JMdict via jmdict-simplified; preserve sense structure. |
| JLPT/common sets | Same pinned snapshots and entry-membership rules as Anki Stats, per section 4. |
| Morphology | First evaluate NMeCab with compatible UniDic data behind a replaceable interface. |
| Built-in grammar | Explicit project rules and reviewed catalog; evaluate OpenJLPT content. |
| Optional generation | Isolated local Ollama HTTP adapter. |
| Packaging | Self-contained Windows x64 app folder, notices, shortcut. |

NMeCab supports dictionary-specific features/adapters; exact UniDic binary/schema
compatibility must be tested. If it fails the evaluation, compare Sudachi through
a bundled worker. Sudachi offers different splitting granularities, but modes
alone do not solve contextual lexical grouping. Any alternative runtime must be
bundled: no user-installed Python/Java for ordinary dictionary use.

Suggested boundaries: App (WPF/view models), Core (lookup/grouping/grammar/stats),
Infrastructure (SQLite/resources/analyzer/Ollama), Checks (fixtures/integration).

Run analysis/downloads/database scans away from UI work. Cancel obsolete requests,
reject stale results, and bound caches by resource/custom-entry revisions.
Load the analyzer once; query indexed dictionary data instead of deserializing
the full dictionary per search.

Proposed measured targets on the user's machine: typical warm sentence analysis
under 250 ms, paragraph under one second, cached definition opening under 100 ms.
Exclude first setup, cold loading, oversized approved input, and local generation.
Measure startup/memory before assigning targets; these are goals, not benchmarks.

### Reuse from Anki Stats

Read-only inspection found C#/.NET 10/WPF, SQLite, pinned/checksummed English
JMdict/OpenJLPT resources, form/reading matching, packaging, and launch scripts.

Adapt useful resource verification, preferred-form selection, packaging, and local
storage conventions. The existing importer condenses glosses and has no morphology
runtime; richer dictionary import and sentence analysis are new work.
Do not copy its large window, Anki features, personal databases, or unrelated stats.
The other project remains unmodified.

Use a similar development approach: documentation in .docs/, focused codex/
branches, meaningful checks, isolated UI test data, and review before merge.
Establish implementation workflow instructions when building is authorized.
No publishing or merging is part of this discovery task.

## 11. Implementation stages and verification

### Stage 0: analysis feasibility

After authorization, build a small experiment before the full UI. Verify resource
formats/notices, offsets/readings, compounds, conjugation chains, and prototype
recognition of しか～ない plus literary/conversational examples. Include held-out
sentences and negative cases. Record package sizes, observed accuracy, and limits;
change analyzer if evidence warrants it. Report any major grammar coverage gap.

### Stage 1: dictionary proof of concept

Implement Search, offline setup, definitions/labels/ruby, roots and supported
conjugation breakdown, custom management and selection lookup, clicked statistics,
basic history, backup/restore, manual resource updates. Explain grammar may expose
only documented prototype-supported constructions at this intermediate milestone.

### Stage 2: useful grammar release

Expand recognition and review compact cards, scope, contractions, and overlaps.
Cover supplied examples plus unseen related VN-style text. Publish the actual
supported patterns and measured limitations. Optional Ollama integration depends
on evaluation and the user's choice; preserve working built-in-only operation.

### Required checks

- 5,000/5,001 character guard; cancellation produces no history event.
- Unicode offsets, punctuation, ruby alignment, IME and text selection.
- Exact custom matches, overlaps, shorter inputs, names, editing/deleting/reanalysis.
- Sense/restriction preservation, labels, root ambiguity, unknown/colloquial text.
- Full-set parity: compare Common and each N5–N1 entry-ID set against Anki Stats
  using the agreed snapshots/rules, including unresolved JLPT rows and entries
  whose common flag occurs only on an alternative written form or reading.
- Conjugation chains and sentence grammar with both positives and counterexamples.
- Repeated clicks, five-count/date threshold, identity grouping, candidate opening.
- History reopen/delete/clear, time boundaries, update/migration identity stability.
- Offline operation, interrupted updates, migration and backup consistency.
- Cancellation/stale results, bounded memory, invalid/unavailable Ollama if enabled.
- Native UI inspection at common display scales, keyboard navigation, long sentences
  and long definitions using isolated synthetic data.

## 12. Review points

Post-preview entry/readability requirements: Ctrl+Plus/Minus changes reading size
by 2 typographic points, with Settings controlling the same persistent
size across search results. Definitions are selectable. An explicit ANKI card
copy action puts the selected dictionary root and reading, e.g. 巨大[きょだい], on
the clipboard; it does not create a note. Common badges are green and JLPT blue,
beneath the word. Dictionary ruby shares the spelling's color and available width,
using natural glyph proportions and spacing, never anisotropic stretching. A
gap/divider separates the headword and senses. Explain grammar sits beneath the
passage legend. Missing Anki kanji cards retain a clickable character and display
unknown; HTML-wrapped exact single-kanji cards must still match. Omit an
Other Forms section when no genuinely different compatible form/reading exists.
Use concise English POS labels and provide a terminology guide in Settings.

The product answers are sufficient to review this design. Remaining items are
proposed defaults or engineering gates, not reasons to postpone the document:

1. Confirm layout, keyboard defaults, download-first setup, and backup scope.
2. Confirm proposed deletion semantics and initial statistics/history views.
3. Native morphology does not automatically explain sentence grammar; Stage 0 must
   establish realistic supported coverage.
4. Literal global custom overrides can misread identical text in another context.
5. Local-model choice requires hardware and Japanese explanation evaluation.
6. Literary names, ellipsis, puns, and fragments can remain ambiguous; preserve
   alternatives and manual selection/custom-entry recovery.
7. Review the distinction between the dictionary proof of concept and useful
   sentence-grammar release before directing implementation.

## 13. Research sources

Reviewed 2026-11-03; capabilities below are source documentation, not a tested app.

- [NMeCab](https://github.com/komutan/NMeCab): .NET morphological analysis and
  dictionary-specific adapters. Record applicable engine and dictionary notices
  separately when pinning dependencies.
- [Sudachi](https://github.com/WorksApplications/Sudachi): alternate splitting
  granularities and dictionary/reading output.
- [jmdict-simplified](https://github.com/scriptin/jmdict-simplified): downloadable
  English JMdict JSON and upstream license references. Preserve source attribution
  and applicable data-license notices.
- [OpenJLPT](https://github.com/evanclan/OpenJLPT): community vocabulary levels and
  candidate grammar reference records. Formation descriptions still require
  recognition logic and independent quality review.
- [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs):
  schema-shaped local API responses.
- [Ollama FAQ](https://docs.ollama.com/faq): local-service configuration guidance.

Pin actual library/data versions and include local attribution/license files
during Stage 0. Keep third-party resources separate from user-authored entries.
