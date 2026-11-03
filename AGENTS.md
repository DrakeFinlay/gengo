# Implementation workflow

Read `.docs/DESIGN.md` for product behavior. The user's implementation request on
2026-11-03 authorizes building with the proposed defaults. Keep work on `codex/`
branches; do not merge or publish without instructions.

Use C#/.NET 10 WPF with Core, Infrastructure, App, and executable Checks projects.
Run `scripts/check.ps1` after substantive behavior changes and build the app.
Use `KOTOBA_DATA_DIR` pointing inside `.tools/` for UI verification; never use
personal history as fixtures. Keep reference data separate from personal SQLite.
Do not modify Anki Stats. Verify its pinned Common/JLPT sets read-only.

Record actual morphology/grammar coverage and gaps in `.docs/IMPLEMENTATION.md`.
Do not replace real analysis with sentence-specific fixtures. Keep all dictionary
senses and restrictions, notices, exact resource pins, and checksum verification.
History and counts change only for intentional successful actions.
