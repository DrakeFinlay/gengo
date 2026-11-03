# Gengo — User's guide

Gengo / 言語 / げんご is a Windows dictionary for reading Japanese passages.
Paste text, search, and choose a word to see its English definitions. Personal
words and names can override the default segmentation and reading. The app also
records deliberate lookups so you can find words you repeatedly need help with.

This guide describes the current preview. Sentence grammar has limited coverage;
Gengo is not a sentence translator. For the implementation, see
[How the program works](program-guide.md).

## Install and open

1. Open `artifacts/Gengo/Gengo.exe` in the repository, or `Gengo.exe` in a copied
   published folder. Keep the whole folder together, including DLLs and licenses.
2. Open **Settings → Install offline resources**. Confirm the download and wait
   for installation/indexing. Expect approximately 62 MiB downloaded and about
   500 MiB on disk, including retained source archives.
3. Return to **Search**. Word lookup, morphology, built-in grammar and character
   dictionary details then work locally. Optional Anki keywords require Anki open.

The published app includes its .NET runtime. Users do not need Python, a browser,
or a separately installed .NET runtime. Building the source requires Windows and
a .NET 10 SDK; see [Tools used](tools-used.md).

Gengo was previously called Kotoba. Its live data still uses
`%LOCALAPPDATA%\Kotoba`. Opening Gengo preserves the entries, settings, history
and resources there. Do not delete that folder because it has the old name.
Old `artifacts/Kotoba` and `artifacts/Kotoba-staging` packages were removed;
launch the current Gengo executable. An already-open app must be closed before
its files can be replaced by a new build.

## Read a passage

1. Paste a word, sentence or paragraph into the input.
2. Press **Enter**, or click **Search**. Shift+Enter inserts a new line.
3. Click an underlined unit in the analyzed passage. Its definitions replace the
   current Words result; kanji cards appear alongside it.
4. Read the first candidate and, when necessary, open another candidate below it.

Search happens only when submitted. Enter used to confirm Japanese IME composition
does not submit. More than 5,000 Unicode characters requires confirmation. Cancel
stops the current operation; a canceled/failed search does not become history.

The input stays above a bounded passage. The Words and Kanji panes scroll
independently. Underlines, colors and gaps distinguish units; the gaps are visual
and are removed when copying or looking up passage selections. Original spaces
and line breaks are retained. Furigana helps with readings but is not proof that
the chosen dictionary sense fits the sentence.

Select passage text and right-click for **Copy**, **Look up selection**, or
**Add personal entry**. Selection lookup is useful when segmentation is wrong.
Keyboard users can focus the passage, move its caret, and press Enter on a unit.
An unknown unit may have no dictionary entry; add a personal expression when needed.

| Shortcut | Action |
| --- | --- |
| Enter in input | Submit Search; Ctrl+Enter is also supported. |
| Shift+Enter in input | Insert a line break. |
| Ctrl+L | Switch to Search and select the input for replacement. |
| Ctrl+Plus / Ctrl+Minus | Change reading size by 2 points; numpad keys work too. |
| Ctrl+B / Ctrl+I in definition editor | Toggle bold/italic. |
| Enter in focused analyzed passage | Open the unit at the caret. |

## Read a dictionary result

Results show the spelling, reading, English senses, grammatical labels, applicable
form restrictions, notes and other forms. Some senses belong only to particular
spellings/readings. Read those restrictions before choosing a meaning.

- **Common word**, green: the source marks at least one spelling/reading common.
- **JLPT N1–N5**, blue: membership in the pinned community vocabulary lists.
- **Personal**: a definition from your own dictionary.
- **Word form**: recognized endings and the dictionary root.

JLPT labels are unofficial vocabulary classifications. An absent tag does not
establish a word's difficulty. **Settings → Dictionary terminology** explains
terms such as futsuumeishi and keiyōdōshi. Source labels are available on hover.

Spelling and meanings can be selected and copied. **ANKI card copy** copies the
selected dictionary spelling/reading as `巨大[きょだい]`. Kana-only entries omit
a redundant reading. Paste it into an Anki Front field yourself; this button
does not create or modify a note.

## Kanji cards and details

The small cards show your exact single-kanji Anki keyword. Click any card to open
independent KANJIDIC2 details: English meanings, Kun/On readings, name readings
(nanori), stroke count and school grade where recorded. Details are bundled with
the app and remain usable without Anki or an Anki card for that character.

| Display | Meaning |
| --- | --- |
| Anki keyword | A matching note in your configured local deck supplied a meaning. |
| `unknown` | No usable exact single-character note was found in that scope. |
| Anki unavailable | The local service, profile check or mapping lookup failed. |
| No character dictionary entry | The separate KANJIDIC2 resource has no record for this character. |

Kun-reading dots mark the start of okurigana; hyphens mark prefix/suffix readings.
Nanori are name readings. The listed possibilities do not all apply to the
selected word. Anki keywords can differ from dictionary meanings; both are shown
with their own source. See [AnkiConnect](anki-connect.md) for setup and diagnosis.

## Personal words and names

1. Open **Personal dictionary → New entry**, or select passage text and choose
   **Add personal entry**.
2. Choose **Personal words** or **Names** in the editor.
3. Enter the expression and English definition; reading and notes are optional.
4. Click **Save entry**. The current passage is reanalyzed without adding another
   search or lookup event.

Use **All**, **Personal words**, and **Names** above the list to browse entries.
Both categories participate in passage matching regardless of the visible filter.
The list shows expressions only; selecting one reveals its reading and definition.
Change its category and save to move it. New entries inherit a Names filter;
otherwise they start as Personal words. Existing pre-category entries default to
Personal words, so move names individually. Expressions are unique across categories.

Matches are literal and global, not tied to a game or paragraph. The longest
matching personal expression at a position takes priority. A custom name can keep
its full spelling together and supply your reading. Notes and Small note text
record context for you; they do not restrict where an override applies.

For English definitions, select text and use **B**, **I**, **1. List**, **• List**,
**Subscript**, or **Small note**. Bold/italic also use Ctrl+B/Ctrl+I inside the
editor. Toggle a format before typing, or apply it to a selection. Small note is
smaller text on the ordinary baseline, suitable for “from game” or a usage label
like Jisho; Subscript places smaller text below it. Formatting is saved, displayed
in search results and included in backups. The separate Notes field is plain text.

To remove an override, select it and choose **Delete entry**, then confirm.
Ordinary matching resumes; previous lookup events retain their identities.

## Explain grammar

Click **Explain grammar** below the passage legend to examine the entire analyzed
passage. It uses a small offline catalog, not AI or a translation service. Current
rules cover しか～ない, という + noun, っていうのに / というのに,
のでもなく / のではなく, なりに, ～ば and ～まい, with some contextual checks.
Use a card's **Highlight in passage** button to highlight its supported span(s).

“None of the built-in grammar rules matched” is an informational result. A passage
can contain substantial grammar beyond these rules. For example, のように and
であり are not currently sentence-grammar rules. Word-form explanations such as
passive or past are a separate mechanism. See [Dictionary and analysis](dictionary-and-analysis.md)
for the precise boundary and [Future plans](FUTURE-PLANS.md) for unfinished work.

## Statistics and history

**Statistics & history** lists words opened at least five times within the selected
date range; choose **All history** to remove date limits. Dates use the computer's
local calendar, with the Through date included. The five-opening threshold is fixed.
Daily totals count deliberate definition openings, not study time or Anki reviews.

Opening A, then A again without changing selection counts once. Opening A → B → A
counts A twice. A new search clears the current selection identity. Scrolling,
grammar, font changes, viewing statistics and reopening saved analysis do not
add lookups. Deliberately opening a definition from a reopened passage can count.
Counts are grouped by dictionary/personal entry identity, not each inflected spelling.

Submitted passages are paged in batches of 30. **Reopen** loads saved analysis
without adding a search. History is retained until you delete it; it is not an
automatic review schedule or a fluency measurement.

| Delete action | Effect |
| --- | --- |
| Delete passage | Removes that text/analysis; keeps lookup counts but removes their sentence links. |
| Delete click | Removes one lookup event and reduces its count. |
| Delete history in selected dates | Removes passages and lookup events in that date range. With no limits, removes all history. |
| Clear all history | Removes all submitted passages and lookup counts; keeps entries and settings. |

Existing backup files are independent copies and are not erased by these actions.

## Settings, backups and data

Ctrl+Plus and Ctrl+Minus change reading size in **2-point steps**, including
numpad keys. **Settings → Reading appearance** controls the same saved size.
It affects the search input, passage and results; it is not a whole-window zoom.

In **Settings → Your data**, create a backup ZIP before moving computers or making
large changes. It contains personal entries (including categories/formatting),
settings, submitted passages and lookup events. Downloaded reference resources
are excluded. Keep the backup private if passages contain private material.

Restore previews the date/counts and asks before replacing current personal data.
It validates the database and creates a `before-restore-*.zip` safety backup first.
It replaces rather than merges. Another computer may need resource installation.
Older schema-1 backups are supported. The schema-2 upgrade creates a
`before-schema-2-*.sqlite` snapshot; older app builds cannot open the upgraded schema.

## Troubleshooting

| Problem | Check |
| --- | --- |
| No ordinary dictionary results | Install offline resources in Settings; retain the entire published app folder. |
| Incorrect segmentation/name reading | Select the desired expression for lookup or save a personal override. |
| Huge な split or unusual inflection | Current grouping is dictionary-validated but limited. Report the exact passage and selected result. |
| Grammar matches nothing | Check supported rules above; an empty result is not a processing error. |
| Anki says unknown/unavailable | Open Anki with AnkiConnect; check active profile, deck and exact Front/Back field names. |
| Changed Anki keyword stays old | Use **Save Anki mapping / refresh**; successful results are cached. |
| Kanji details missing | Click the card even when Anki is unknown. If bundled data is unavailable, replace the full app package. |
| App appears empty after moving/renaming | Check Settings's personal-data path and any inherited `KOTOBA_DATA_DIR`; do not delete the old-name live data folder. |
| Definition controls extend below window | Scroll the editor pane; its scroll position is independent of the entry list. |
| Update check finds no newer release | It checks availability of the supported pin, not upstream's newest data or an app update. |

If reporting lag, include input length, the action that triggers it, whether it
continues while idle, and resource versions. Avoid sharing a personal database
unless you intend to share all entries/history. See [How the program works](program-guide.md)
for storage and responsiveness details.
