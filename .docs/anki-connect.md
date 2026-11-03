# AnkiConnect integration

Gengo optionally reads kanji keywords from the running local Anki profile. It does
not share Anki Stats's collection import or note-writing workflow. Ordinary word
lookup and bundled character details work without this connection.

## Setup and user-visible behavior

Open Anki with AnkiConnect enabled. In **Settings → Kanji meanings from Anki**,
set the deck and exact field names. Defaults are deck **漢字**, expression field
**Front**, meaning field **Back**. The deck scope includes child decks. Choose
**Save Anki mapping / refresh** after changing mappings or editing cached keywords.

Choosing a dictionary word immediately creates one card per distinct kanji, then
fills them asynchronously. The source label identifies the active Anki profile and
configured deck. A failed local connection does not block the word definition.
Click the card for KANJIDIC2 readings/meanings even when Anki is unavailable.

`unknown` means there is no usable exact card in this scope, including cases where
the expression matches but its configured meaning is empty. It does not mean the
character is absent from all your decks or from the character dictionary.

## Transport and allowed API actions

`src/Kotoba.Infrastructure/AnkiKanjiClient.cs` uses HTTP POST
to `http://127.0.0.1:8765/`, AnkiConnect API version 6, with an eight-second timeout.
Proxy use and HTTP redirects are disabled. There is no configurable remote endpoint.

| Action | Purpose |
| --- | --- |
| `getActiveProfile` | Identify the source and verify it did not change during lookup |
| `findNotes` | Find candidate notes in the configured deck/field |
| `notesInfo` | Read fields for the returned candidates |

Those three actions are the client's explicit allowlist. No addNote, update,
delete, review, template change, collection-file access or sync operation is used.
No Anki collection, cards, templates or media are bundled with Gengo.

## Why candidate search is broad but matching is exact

A stored Front can be HTML such as `<div>分</div>`. A narrow Anki field query can
miss that note. Gengo searches candidates with a quoted deck term and wildcarded
field, for example `"deck:漢字" "Front:*分*"`, then checks cleaned content exactly.

Cleaning removes script/style contents, ruby `rt` readings, tags and sound markers;
it preserves useful line breaks, decodes HTML entities and trims surrounding space.
This is text cleaning, not an HTML/JavaScript execution surface. A cleaned Front
must equal the single requested kanji. 分解 cannot supply 分's keyword, nor 床屋
supply 床's. Meanings come from cleaned stored fields rather than rendered templates.
Multiple matching notes retain distinct meanings.

Requests accept one kanji, validate deck/field syntax and reject more than 500
candidate notes. Fields above 100,000 characters are rejected. These failures are
reported as unavailable/mapping issues rather than quietly inventing a keyword.

## Caching, cancellation and failures

Successful results use a bounded 256-entry cache keyed by active profile, mapping
options and character. Missing results are not cached, so a newly added exact note
can appear on the next selection. Mapping save/refresh clears successful results.
Profile is checked before and after reading; a mid-request profile switch fails
instead of mixing fields from different profiles. Changing selected words cancels
pending work so stale replies cannot replace the new selection's cards.

If a keyword is wrong, inspect the stored expression/meaning fields and current
mapping. A different template's rendered text is not necessarily the mapped field.
If no cards load, check AnkiConnect is enabled, Anki is open and the expected profile
is active. Retry after changing profile or fixing the mapping.

## Validation and privacy

Checks use a local fixture server to verify allowed actions, exact matching,
HTML-wrapped fields, ruby removal, compound exclusion, cancellation, missing cards,
cache/profile separation and service errors. Optional `--anki-probe` is a deliberate
read-only development command against live Anki, not part of normal synthetic checks.

The requested character/deck query is sent to the local service; Gengo does not
send a whole passage or definitions to a cloud model. **ANKI card copy** only writes
the chosen spelling/reading to the Windows clipboard and does not call this client.
