# Third-party components and data

Gengo uses separate, replaceable dictionary resources. Personal entries and
history are stored independently. Dictionary installation sends no personal text.

- .NET 10 / WPF © Microsoft and contributors, MIT. See `LICENSES/dotnet.txt`
  and `LICENSES/dotnet-third-party.txt`.
- Microsoft.Data.Sqlite 10.0.14, MIT; `LICENSES/microsoft-data-sqlite.txt`.
- Microsoft.IdentityModel.JsonWebTokens, Tokens, Logging and Abstractions 8.23.0,
  © Microsoft Corporation, MIT; `LICENSES/identitymodel.txt`. Used for signed
  OpenID Connect identity-token validation. Source commit
  `8b16f418b3d587416d293e9ba34c5d310781cdb3` in
  [IdentityModel Extensions](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet).
- System.Security.Cryptography.ProtectedData 10.0.14, Microsoft/.NET, MIT;
  `LICENSES/dotnet.txt` and `LICENSES/dotnet-third-party.txt`. Windows DPAPI
  protects Gengo's own ChatGPT credentials for the current Windows account.
- SQLitePCLRaw 3.0.5, Apache 2.0; `LICENSES/sqlitepclraw.txt`. SQLite is public
  domain: <https://sqlite.org/copyright.html>.
- NMeCab / LibNMeCab 0.10.2 © Tsuyoshi Komuta, a C# port of MeCab. Gengo
  uses the LGPL 2.1 license option. See `LICENSES/NMeCab-COPYING.txt` and
  `LICENSES/NMeCab-LGPL-2.1.txt`. Source and replacement library:
  <https://github.com/komutan/NMeCab>. The library remains a separate
  `LibNMeCab.dll`; a compatible modified library can replace it. No NMeCab
  source modifications are made by this project.
- UniDic-lite 1.0.8, Paul O'Leary McCann and contributors, MIT for package code;
  extracted UniDic 2.1.2 dictionary © 2011–2017 UniDic Consortium, BSD.
  `LICENSES/UniDic-BSD.txt`, `LICENSES/UniDic-lite.txt`.
  <https://github.com/polm/unidic-lite>. Only binary dictionary files and notices
  are used; ordinary app use requires no Python installation. The pinned archive
  incorporates the upstream UniDic-lite changes documented in its README.

## Dictionary resources

- KANJIDIC2 © James William Breen and EDRDG, CC BY-SA 4.0.
  [KANJIDIC project](https://www.edrdg.org/wiki/KANJIDIC_Project.html),
  [EDRDG license](https://www.edrdg.org/edrdg/licence.html).
  Bundled unchanged jmdict-simplified `kanjidic2-all` release
  `3.6.2+20261026175559`, dictionary date 2026-10-26, file version 4,
  database version 2026-299; 13,108 characters.
  `src/Gengo.Infrastructure/Resources/kanjidic2-all.json.zip` SHA-256:
  `4C5969C69A5A3B21F87254A440D3BBC5E92420793BB22D78E4CCAA603AEC2501`.
  [Exact source archive](https://github.com/scriptin/jmdict-simplified/releases/download/3.6.2%2B20261026175559/kanjidic2-all-3.6.2%2B20261026175559.json.zip).
  The archive is embedded unchanged; runtime presentation selects English meanings
  and Japanese readings, retaining meaning/reading groups, reading qualifiers,
  nanori, stroke counts and school grade. Other source fields remain in the archive.
  Legacy kanji JLPT numbers are not displayed as modern N-levels.
  Full license: `LICENSES/CC-BY-SA-4.0.txt`.
- JMdict © Electronic Dictionary Research and Development Group (EDRDG),
  started by Jim Breen. [JMdict project](https://www.edrdg.org/jmdict/j_jmdict.html),
  [EDRDG license](https://www.edrdg.org/edrdg/licence.html), CC BY-SA 4.0.
- [jmdict-simplified](https://github.com/scriptin/jmdict-simplified), scriptin
  and contributors, English release `3.6.2+20261026175559`. Gengo indexes
  spelling/reading metadata, all English senses, sense restrictions and labels.
  The imported structure is adapted into a SQLite index; example collections
  are not imported. Common classification unions all written/reading flags.
- [OpenJLPT](https://github.com/evanclan/OpenJLPT) contributors, revision
  `0d1d3410bec90bd4098a7c72de820543cb4f707c`, CC BY-SA 4.0, derived from
  [Jonathan Waller's JLPT Resources](https://www.tanos.co.uk/jlpt/) (CC BY)
  and JMdict. Only explicit vocabulary ID/level memberships are imported;
  unresolved rows remain unlinked. See `LICENSES/OpenJLPT-NOTICE.md`.

The local dictionary index and derived reference classifications use CC BY-SA
4.0; full text is included at `LICENSES/CC-BY-SA-4.0.txt`. Keep these notices
with redistributed reference data. These terms do not label personal entries,
personal history, or application code as dictionary-source data. JLPT lists
are unofficial community estimates, not official examination inventories.

Built-in English grammar explanations are project-authored. No third-party
grammar catalog, sentence translations, cloud model, or Ollama model is bundled.

Optional kanji lookup interoperates read-only with the separately installed
[AnkiConnect add-on](https://ankiweb.net/shared/info/2055492159) through the local
loopback API. No Anki or AnkiConnect code, cards, or collection files are bundled.
Meanings labeled Anki are the user's own deck fields, not JMdict-derived kanji
keywords. This project does not alter the source deck.
