# Development workflow

These are Gengo's contribution rules, adapted to this repository rather than
copied from Anki Stats's release workflow. `AGENTS.md` is the local
implementation instruction source. Follow explicit user instructions when scope
or publishing authorization changes.

## Inspect before changing

Read [DESIGN.md](DESIGN.md) for agreed behavior and
[IMPLEMENTATION.md](IMPLEMENTATION.md) for actual coverage and gaps. Use the
[repository map](repository-structure.md) to find the relevant layer. Inspect Git
status and branch before editing. Work on `codex/` branches. Do not merge, push or
publish remotely without instructions; local self-contained packaging is part of
development, not permission to distribute a release.

Treat Anki Stats as a read-only reference. Its resource pins and classification
baseline are shared compatibility inputs, not files to rewrite from this project.
The documentation's future plans are not instructions to implement extra scope.

## Make a coherent change

Keep Core independent of WPF/storage. Preserve original text spans, every imported
sense/restriction, resource notices and exact checksum verification. Keep reference
resources separate from personal SQLite. Counts/history must change only for
intentional successful actions. New analysis must use general rules/evidence,
not sentence-specific answers added to make screenshots look correct.

Personal schema changes require a safety snapshot before migration and meaningful
checks for old data and backup compatibility. Do not use live personal history as
a fixture. For UI verification, set `KOTOBA_DATA_DIR` inside this repository's
`.tools/` directory. Explicit live Anki probes are read-only and separate from
ordinary synthetic checks.

## Validate at the appropriate level

Run `scripts/check.ps1` after substantive behavior changes and build the app.
Core checks create generated temporary data. Full-data checks require an isolated
reference installation and can compare the complete Common/JLPT sets with Anki
Stats. UI checks create a real WPF window and modify their isolated fixture.

```powershell
$sdk = 'C:/path/to/dotnet.exe'
./scripts/check.ps1 -DotnetPath $sdk
# Optional explicit download/indexing test and fixture preparation:
& $sdk tests/Kotoba.Checks/bin/Debug/net10.0/Kotoba.Checks.dll --install .tools/test-data
# Full resources, parity (optional baseline) and native WPF checks:
./scripts/check.ps1 -DotnetPath $sdk -ReferenceDirectory .tools/test-data `
  -ParityBaseline 'C:/path/to/coverage-reference-v1.json.gz' -Ui
./scripts/build.ps1 -DotnetPath $sdk -Publish
./scripts/launch.ps1 -DataDirectory .tools/test-data
```

`--prepare <dictionary-source-directory> <unidic-archive> <fixture-directory>`
can build an isolated fixture from existing pinned sources. `--anki-probe <kanji>`
contacts live local Anki; it is not a synthetic test. See
[IMPLEMENTATION.md](IMPLEMENTATION.md) for observed commands and installation evidence.

Check idle redraw behavior as well as initial search latency. Verify narrow/tall
layouts and selection/clipboard behavior when changing text UI. A timing-only
failure should be investigated against startup/concurrent activity and the redraw
counters; do not hide it by blindly weakening thresholds. Once relevant checks
pass, avoid repeating unrelated suites without a reason.

Documentation-only edits need source/behavior review, valid local links and verified
packaging paths; they do not require analyzing a personal collection or launching
the GUI. Build/publish script edits should at least be parsed for PowerShell syntax.

## Package and report

Close running app instances before replacing their locked binaries; preserve any
unsaved work. Publish creates `artifacts/Gengo/Gengo.exe` and copies licenses,
README and `.docs/`. It is a snapshot of that build, not an automatically rebuilt
shortcut. The launcher clears inherited test-data overrides unless an explicit
`-DataDirectory` is provided.

Update IMPLEMENTATION with real grammar/morphology coverage and limitations.
Update user-facing guides when controls or storage behavior change. Report the
result, relevant verification and remaining material limitations. Remote PR/merge
steps require separate authorization; do not inherit Anki Stats's publication
procedure merely because its documentation was used as a reference.
