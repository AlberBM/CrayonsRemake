# Crayons Studio Remake

A native Windows 10/11 recreation of the 1997 children's paint program **Crayons Color & Paint Studio**. The original is a 16-bit Windows 3.1/95 program that no longer runs on 64-bit Windows.

The remake reproduces the original's screens, tools, music and sound effects. Both age levels are included: Younger and Older. That means lines, paint, wacky brushes, shapes and 3D shapes, text styles, erasers, Change It effects, stamps, coloring pages, the Jukebox and the rest of the studio.

> **You need your own copy of the original program.** This repository contains no artwork, sounds, music or other files from the original. The remake reads them at run time from your installed copy, the same way ScummVM uses a game's original data files.

## Requirements

- Windows 10 or 11 (x64).
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) to run it, or the .NET 8 SDK to build it.
- An installed copy of Crayons Color & Paint Studio: the folder that contains `CRAYON1.EXE`, `CRAYON2.EXE`, `A1.ASL` and the `SND`, `ART` and `ACTIVITY` folders.

The remake looks for that folder in `\XCRAYON`, `\Programs\XCRAYON` and `\Program Files\Expert Software\Crayons` on each fixed drive. If it doesn't find one there, it asks you to pick the folder once and remembers your choice in `%LOCALAPPDATA%\CrayonsRemake\install-path.txt`. You can also pass the folder as the first argument.

## Download

If you don't want to build it yourself:

1. Get `CrayonsStudioRemake-win-x64.zip` from the [latest release](https://github.com/AlberBM/CrayonsRemake/releases/latest).
2. Unzip it anywhere.
3. Run `Crayons.App.exe`.

The download is self-contained, so you don't need to install .NET. On first start the program asks for your original Crayons folder if it doesn't find it on its own.

Windows SmartScreen may warn about an unrecognised app because the exe isn't code-signed. Choose **More info → Run anyway**.

## Build and run

```
dotnet build -c Release
dotnet test
dotnet run --project src/Crayons.App -c Release
```

To produce a standalone folder you can copy anywhere:

```
dotnet publish src/Crayons.App -c Release -r win-x64 --self-contained false -o publish
publish\Crayons.App.exe
```

Command-line options:

| Option | Effect |
|---|---|
| `<folder>` | Use this original install folder. |
| `--windowed` | Start in a window instead of full screen. **F11** toggles between them at any time. |
| `--level=younger` / `--level=older` | Skip the splash screen and the age picker. |

At build time the program's icon is copied from `CRAYONST.EXE` in your install (`E:\Programs\XCRAYON` by default; change it with `-p:CrayonsInstall=<folder>`). Without an install the exe gets the default icon.

## How it works

The 640x480 screen is drawn with real Win32 GDI calls into a 32-bit bitmap, so lines, fills, hatch brushes and text come out the same as in the original. The bitmap is then scaled up with nearest-neighbour scaling and letterboxed to fit your display.

What comes from the original files, and when:

| What | Where it comes from |
|---|---|
| Bitmaps, sound effects and MIDI music | The install folder, at run time |
| Dialog layouts, cursors and icons | Read straight out of the original program files |
| Random-brush effects | A reimplementation of the original's random-number generator |

Music plays through MCI and sound effects through `PlaySound`.

- `src/Crayons.Core`: platform-independent logic, such as the asset readers, the studio state and the drawing maths for each tool.
- `src/Crayons.App`: the WinForms host, rendering, audio, screens, tools and dialogs.
- `tests/Crayons.Tests`: unit tests (xUnit). Tests that need an original install skip themselves when there isn't one.
- `docs/decisions.md`: every design decision, with its reasons.
- `docs/open-questions.md`: behaviour that is still unknown or was settled later.

The remake was built clean-room from a written description of the original's behaviour, checked against the running original side by side. It contains no code from the original program.

## Known limitations

- **Help** opens the original `CRAYONS.HLP` only when a WinHelp viewer (`winhlp32.exe`) is available. Otherwise it opens `XCRAYONS.PDF` if present.
- **Printing** uses the default Windows printer. It has not been tested against the original's print layout.
- The machine-specific paths in the source, such as the default install folder and the reference files used by two tests, are only fallbacks. Everything still works without them.

## Disclaimer

This is an unofficial fan project for preservation and education. It is not affiliated with or endorsed by the original publisher or the owners of the Crayons Color & Paint Studio name. All trademarks belong to their respective owners. No original assets are distributed in this repository. The one exception is the program icon embedded in the release download's `Crayons.App.exe`, which is the original program's icon.
