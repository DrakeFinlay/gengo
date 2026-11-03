# Gengo — Future plans and unfinished scope

This page separates unimplemented work from current features. It is not a promise
of release dates or authorization to implement every item. Current operation is in
the [user's guide](USER-GUIDE.md); actual experiments/checks are in
[IMPLEMENTATION.md](IMPLEMENTATION.md).

## Useful grammar release

The design has two milestones: a dictionary proof of concept and a useful grammar
release. The current app is a runnable preview with a small `builtin-1` grammar
catalog. Sentence grammar remains a core goal; broad coverage is unfinished.

Next work should expand general construction rules using real token/context
evidence, support separated components where justified, retain ambiguity and
highlight exact original spans. Current gaps include のように, であり, broader
clause linkage and nominalization combinations. These examples identify gaps,
not a request to hardcode one sample passage.

Acceptance needs an independently selected passage set, negative controls,
documented precision/coverage and linguistic review. Passing authored regression
examples alone does not establish a useful literary grammar release. Unsupported
constructions must remain distinguishable from processing errors.

## Morphology, names and reading quality

Dictionary-backed grouping and supported endings work now, but compound choices,
literary inflections and proper-name readings can still fail. Improve evidence
and uncertainty reporting without losing senses, restrictions or source offsets.
Keep personal literal overrides as a predictable correction path.

Context-specific override rules, fuzzy personal matching and automatic custom
conjugation generation are not implemented. They would need explicit behavior for
collisions and exceptions before replacing today's global literal precedence.

## Configurable personal categories

Personal words and Names are implemented. User-created/renamed/reordered categories
are deferred. A later design should settle stable IDs, moving/deleting nonempty
categories, filter behavior, migration, backup compatibility and whether any
category should affect matching. Currently both categories participate equally.

## Optional local-model grammar mode

DESIGN discusses optional Ollama use. This build has no model installation,
selection, inference path or fallback to one. Any such addition needs a deliberate
choice about local runtime requirements, resource use, uncertainty, text handling,
cancellation and comparison with built-in rules. It must not silently replace
the offline authored mechanism or claim unsupported interpretation as fact.

## Release readiness and updates

Continue full-data/parity checks, UI performance checks and representative manual
reading sessions. Resource updates must coordinate Common/JLPT pins with Anki
Stats; independently grabbing upstream latest would break that contract.
The current update button is supported-pin availability checking, not automatic
application updating or a newer-baseline installer.

An installer, automatic updates, release distribution/signing and shortcut creation
are not implemented by the current build/launch scripts. Local publishing produces
a self-contained folder. Any future release work must preserve licenses, compatible
data migration and a clear distinction between binaries and live personal data.

## Outside the current scope

The agreed initial exclusions include audio, example-sentence collections,
English/romaji search, OCR, clipboard monitoring, translation and Anki writes.
Existing ANKI card copy is clipboard export only. Existing formatting/category
features do not imply media libraries or per-game dictionaries. These directions
need product decisions before being treated as planned implementation.
