# Tools used

Gengo is a local Windows desktop application. This page describes the current
stack, not additional software the user needs to install for ordinary use.

## Application and data stack

| Tool/resource | Use |
| --- | --- |
| C# and .NET 10 | Application, analysis, infrastructure and executable checks |
| WPF | Native desktop controls, selectable documents, ruby drawing and rich-text editor |
| Microsoft.Data.Sqlite 10.0.14 | Indexed reference data and personal SQLite persistence |
| SQLitePCLRaw.bundle_e_sqlite3 3.0.5 | SQLite native runtime distribution |
| LibNMeCab 0.10.2 | Managed MeCab implementation used through a UniDic adapter |
| UniDic-lite 1.0.8 / UniDic 2.1.2 | Morphological dictionary |
| JMdict, OpenJLPT, KANJIDIC2 | Pinned word/level/character reference data |
| .NET HTTP, JSON, ZIP/TAR and cryptography APIs | Local integration, downloads, serialization, archives and SHA-256 verification |
| PowerShell scripts | Repeatable build, publish, check and launch commands |

No Python interpreter, browser runtime, database server, cloud model or Ollama
installation is required. No third-party rich-text component is used; the editor
and RTF support come from WPF. Optional Anki integration requires the user's
separate Anki/AnkiConnect installation.

## For users

Use the complete self-contained Windows x64 published folder. Word analysis needs
the first explicit resource installation. Character details are bundled. Refer to
[USER-GUIDE.md](USER-GUIDE.md) for setup, data paths and recovery.

## For developers

Use Windows, PowerShell and a stable .NET 10 SDK compatible with `global.json`.
The project does not install a system SDK or modify PATH. Pass an SDK executable
when machine-wide `dotnet` resolves to an obsolete preview. NuGet/CLI state stays
under `.tools/packages` and `.tools/cli` when using the scripts.

From the repository root:

```powershell
$sdk = 'C:/path/to/dotnet.exe'
# Compile Debug and refresh the stable self-contained build/root shortcut.
./scripts/build.ps1 -DotnetPath $sdk
# Run core/infrastructure checks.
./scripts/check.ps1 -DotnetPath $sdk
# Refresh the same stable folder/shortcut without the extra Debug compile.
./scripts/build.ps1 -DotnetPath $sdk -Publish
# Launch the latest successful stable build; this does not rebuild it.
./scripts/launch.ps1
```

The current environment has used the read-only SDK at
`D:/Drake/git-projects/anki-stats/.tools/dotnet/dotnet.exe` (10.0.403).
That is an environment-specific SDK location, not an application runtime
dependency or a requirement to install Anki Stats.

For full installed-resource/UI checks and fixture preparation, see
[Development workflow](development-workflow.md). Scripts use single-process build
and disable shared compilation for predictable local execution. Dependency restore
may need network access; fixture-only analysis does not.

## Verification and licensing

Checks are executable .NET projects, not a browser test suite. They cover behavior,
SQLite recovery, local HTTP fixtures, source parity, full dictionary morphology
and native UI rendering. Timing is reported as measured evidence, not a guaranteed
latency on every machine. Source/field restrictions and negative controls matter
as much as positive examples.

See [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) and `LICENSES/` for license
texts and exact pins. NMeCab remains a separate DLL so compatible replacements
are possible; the application is published as a folder rather than a single-file
bundle. These source-data licenses do not turn personal history into public data.
