# Dictionary resources and Japanese analysis

For everyday use see the [user's guide](USER-GUIDE.md); for the full pipeline see
[How the program works](program-guide.md). This guide distinguishes source data
from Gengo's interpretation. Resource notices and exact archive hashes are in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) and
`src/Kotoba.Infrastructure/ReferenceStore.cs`.

## Sources and pins

| Resource | Pinned version | Role |
| --- | --- | --- |
| JMdict English via jmdict-simplified | `3.6.2+20261026175559` | 218,963 word entries, spellings/readings, English senses and metadata |
| OpenJLPT | `0d1d3410bec90bd4098a7c72de820543cb4f707c` | Explicit community vocabulary entry/level memberships |
| UniDic-lite | `1.0.8`, containing UniDic 2.1.2 | Morphological dictionary used by NMeCab 0.10.2 |
| KANJIDIC2 via jmdict-simplified | `3.6.2+20261026175559`, date 2026-10-26, file version 4, database 2026-299 | 13,108 character records, independent of words and Anki |

Word setup downloads a JMdict ZIP, five level JSON files and UniDic's archive.
The installer checks each pinned SHA-256, safely extracts the expected morphology
files, builds the SQLite index and activates the completed version. A cancellation
or failed checksum does not activate partially installed resources. Installation
retains source archives; disk use exceeds the compressed download size.

Character data is already embedded as the unchanged `kanjidic2-all` ZIP in
Infrastructure. Its SHA-256 is
`4C5969C69A5A3B21F87254A440D3BBC5E92420793BB22D78E4CCAA603AEC2501`.
`src/Kotoba.Infrastructure/KanjiDictionary.cs` verifies the
archive before parsing once on a worker thread. The all-language archive retains
characters without English meanings; presentation selects English meanings and
Japanese readings, preserving grouping, qualifiers, nanori, strokes and grade.
Legacy kanji JLPT levels are omitted rather than mislabeled as modern N-levels.

The manual update action probes the supported pin. It does not independently
advance sources: word classifications must stay coordinated with Anki Stats.
Bundled KANJIDIC2 changes arrive with an app build, not word-resource reinstall.

## Reference index and personal storage

`reference/current.txt` selects an immutable `resources-*` installation containing
`reference.sqlite` and `UniDic/`. The reference index stores entries, indexed forms,
labels and import metadata. Lookups use indexed equality and bounded caches rather
than scanning/parsing the full dictionary for every token. Reference data is
replaceable and separate from `personal.sqlite`; it is not included in personal backups.

All English senses, spelling/reading restrictions, notices, source labels and
other forms are retained. A sense restriction is not discarded just because the
selected spelling is more common. UI labels shorten source terminology without
changing its underlying meaning. Dictionary terminology is available in Settings.

## Common/JLPT compatibility with Anki Stats

Common is the union of common flags across written and reading forms. JLPT data
links explicit vocabulary IDs rather than guessing a level from a character.
The verified baseline contains:

| Set | Unique linked JMdict IDs |
| --- | ---: |
| Common | 22,649 |
| N1 | 3,056 |
| N2 | 1,760 |
| N3 | 1,653 |
| N4 | 630 |
| N5 | 669 |

Eleven source JLPT rows remain unresolved/unlinked. Parity checks compare full ID
sets against the supplied Anki Stats baseline, not just matching totals. Gengo
does not modify that project. Matching classifications does not mean Gengo has
Anki Stats's coverage cohorts, collection import or reports; those are separate features.

The classifications are community estimates. A missing tag is not evidence of
advanced difficulty, and live Jisho classifications may differ from the fixed pin.

## Segmentation, candidate ranking and endings

1. Find literal personal overrides, longest first at a given position.
2. Parse remaining text with UniDic, retaining original token offsets and whitespace.
3. Consider bounded multi-token dictionary spans with reading/part-of-speech evidence.
4. Prefer validated verb/adjective roots where appropriate; recognize supported
   potential-root relationships and adjacent auxiliary chains.
5. Group a dictionary-backed na-adjective with its contiguous attributive な.
6. Rank candidates by reading evidence and commonness, while exposing alternatives.

Internal tokens remain available beneath display groups. 巨大な can open 巨大
while preserving な's morphology. A recognized lexical expression can include a
particle without absorbing unrelated standalone particles. Whitespace/punctuation
boundaries constrain merging. Personal entries match literally, with no automatic
fuzzy matching or custom conjugation generation.

Recognized endings can describe passive, potential, causative, negative, completed
past, politeness, connective forms, conditionals and selected auxiliaries. Some
forms overlap in meaning; the UI retains uncertainty. A valid root/reading match
does not decide which English sense is correct in context. Names, literary forms,
unusual compounds and malformed input can remain incorrect or unresolved.

## Sentence grammar coverage

The `builtin-1` catalog is project-authored code, not a third-party grammar dataset
or an LLM. It recognizes しか with a locally supported negative predicate,
っていうのに / というのに, のでもなく / のではなく (including supported
contractions), token-checked なりに, という followed by noun evidence, supported
～ば inflections (including a 気が付けば interpretation), and ～まい.

This is limited coverage. であり, のように, passive clause linkage and 事だけ
are examples of constructions not currently covered as sentence-grammar rules.
Morphological analysis of a passive ending can succeed while sentence grammar
returns no cards. An empty result means no current rule matched, not an absence
of grammatical structure.

Authored positive/negative examples are regression evidence, not an independently
measured coverage rate. Broader acceptance still requires held-out passages,
negative controls and linguistic review. See [Future plans](FUTURE-PLANS.md) and
[IMPLEMENTATION.md](IMPLEMENTATION.md); do not substitute hardcoded sentence
responses for general analysis.

## Verification and provenance

Checks cover source-span reconstruction, literal override precedence, supported
inflections, restrictions, grouping and grammar positives/negative controls.
Full-data runs add complete classification parity and analysis timing. UI checks
cover rendering/selection and idle behavior. Checks also verify character details
for 地/面/分 and readings in records without English meanings.

Source licenses/notices accompany the app. The derived word reference index uses
CC BY-SA 4.0; personal entries/history are separate user data. Keep notices when
redistributing resource data. No downloaded code, sentence-specific lookup service,
or user text transmission is needed for normal analysis.
