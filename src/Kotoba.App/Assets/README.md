# Gengo icon

An ivory 言 (word/speech) on a deep teal rounded tile. Colors: `#FFF8E8` and
`#246F61`; transparent outside the rounded tile. The single bold character favors
legibility at taskbar sizes and matches the application's existing teal palette.

- `Gengo.svg`: scalable source, with the glyph stored as vector outlines; no font
  is required to display it. The outline was produced from Windows Yu Gothic UI
  Bold; no font file is embedded or distributed.
- `Gengo.png`: 512×512 transparent preview.
- `Gengo.ico`: RGBA PNG frames at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels.

The project `ApplicationIcon` embeds the ICO in the executable's Windows resources.
The same ICO is a WPF Resource explicitly assigned to `MainWindow.Icon` for window,
Alt+Tab and taskbar presentation. Update both uses together when revising the mark.
