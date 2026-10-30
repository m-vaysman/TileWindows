# Tile Windows

<p align="center">
  <img src="assets/icon.svg" alt="Tile Windows icon" width="128" />
</p>

**Double-click** desktop utility for Windows. It tiles the visible windows on your **current** virtual desktop across the primary monitor work area (above the taskbar). Windows on other desktops are left alone.

![Before and after tiling](assets/before-after.svg)

## How it works

```mermaid
flowchart LR
  A[Double-click Tile Windows] --> B[Find visible titled windows]
  B --> C[Skip minimized / tool / shell]
  C --> D[Read work area]
  D --> E[Restore + resize into grid]
```

### Layout rules

| Open windows | Layout |
| ---: | --- |
| 1 | Fills the work area |
| 2+ | Columns sized to the screen's shape (about 16:10 tiles), rows balanced as evenly as possible |

Every row is split across the full width, so a short row stretches its windows instead of leaving an empty cell. On a 5120x1440 ultrawide, up to 5 windows sit side by side in one row.
```text
  2 windows              3 windows              4 windows
┌─────────┬─────────┐  ┌────┬────┬────┐  ┌─────────┬─────────┐
│         │         │  │    │    │    │  │         │         │
│    1    │    2    │  │ 1  │ 2  │ 3  │  │    1    │    2    │
│         │         │  │    │    │    │  ├─────────┼─────────┤
└─────────┴─────────┘  └────┴────┴────┘  │    3    │    4    │
                                         └─────────┴─────────┘
```

## Use

1. Build (or copy) `Tile Windows.exe` to your Desktop.
2. Open the apps you want arranged.
3. Double-click **Tile Windows**.

No installer. No admin rights. Runs silently.

## Build

Requires .NET Framework 4.x `csc` (included with Windows / Visual Studio Build Tools):

```bat
build.bat
```

Or manually:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /out:"Tile Windows.exe" TileWindows.cs
```

Optional: set the shortcut icon to `assets/icon.ico`.

## Behavior notes

- Operates on the **active** virtual desktop only (cloaked windows are skipped).
- Skips minimized windows, untitled windows, tool windows, the desktop shell, and fixed-size dialogs that can't be resized.
- Restores maximized windows before moving them.
- Uses the primary monitor **work area** so the taskbar is not covered.
- DPI aware, and accounts for the invisible Windows 10/11 resize border, so tiles fill the screen edge to edge.
## License

MIT — see [LICENSE](LICENSE).
