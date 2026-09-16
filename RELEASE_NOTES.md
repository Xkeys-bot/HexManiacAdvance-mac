# HexManiacAdvance for macOS - beta 1

A macOS build of [HexManiacAdvance](https://github.com/haven1433/HexManiacAdvance). Upstream is a
WPF application and runs on Windows only; this is a port of that UI to Avalonia, with a few
additions. **HexManiac.Core is unmodified**, so the editing behaviour - how it reads tables, repoints
data, and keeps undo history - is upstream's, unchanged.

This is a **beta**. It has been used in earnest on exactly one machine with one ROM. It is here so
other people can break it in ways I could not.

## Install

1. Download the build for your Mac: **arm64** for Apple Silicon, **x64** for Intel.
2. Unzip and drag `HexManiacAdvance.app` to Applications.
3. Clear the download quarantine - **this step is not optional**:

```
xattr -dr com.apple.quarantine /Applications/HexManiacAdvance.app
```

The app is ad-hoc signed but **not notarized**, so macOS refuses to open it until that flag is gone,
and on recent versions right-click Open no longer clears it. Without this you get "damaged and
can't be opened", which is macOS being unhelpful rather than the file being damaged.

Requires macOS 11 or later. Nothing else to install - the build is self-contained and does not need
.NET.

Open a ROM by double-clicking a `.gba`, dropping one on the app, or File Open.

## What's in it

Everything upstream does - Pokemon, trainers, moves, items, maps, scripts, images, the pokedex, the
goto tool, the utilities menu - plus:

- **A built-in emulator.** *File Test in Emulator* runs the ROM **as it stands in the editor**,
  unsaved edits included. Nothing is written to your `.gba` to test a change.
- **Named save states**, so you can play to the spot you care about once and jump back to it after
  every edit instead of walking there again.
- **A live memory viewer** that watches the running game's RAM and highlights what changed, with
  snapshot/compare for finding what a flag or variable actually is.
- **Project export/import** - every table as tab-separated text plus a manifest, so a hack can live
  in git, be diffed, and be reviewed. The round trip is lossless; import lands as one undo entry.
- **An MCP server and a chat panel**, so Claude can read and edit the open ROM through the editor's
  own model and undo history - meaning you can undo anything it does. Both are **off until you turn
  them on**; the server binds to loopback only and checks request origins. The chat panel uses an
  installed [Claude Code](https://claude.com/claude-code) CLI, so it runs on your existing
  subscription and needs no API key.

## The emulator needs a core

The app does not ship an emulator. It loads a libretro core from an installed copy of
[RetroArch](https://www.retroarch.com) - install RetroArch, grab a GBA core through its updater, and
HexManiacAdvance will find it. Without one, the emulator window says so and offers to browse for a
core file.

Which core you pick matters more than it sounds:

| | mGBA | VBA-M / VBA-Next / gpSP |
|---|---|---|
| Accuracy | best | good |
| Reset button | works | **does nothing** - use a save state |
| Live memory | IWRAM only (32 KB) | EWRAM (256 KB) - flags, variables, party |

Changing cores takes effect the next time you start the editor: two libretro cores cannot share one
process.

## Known issues

- **Reset does nothing on VBA-M.** That core keeps global state across a restart, so even a brand
  new core instance carries on where it was. There is no fix on this side. Use a save state, or use
  mGBA.
- **Not notarized.** Hence the `xattr` line above. Signing it properly needs a paid Apple developer
  account.
- **The Intel build is untested on real Intel hardware.** It was built and smoke-tested under
  Rosetta on Apple Silicon. If you are on an Intel Mac, you are the first - please say how it goes.
- **The automated test suite has never run against this port.** The upstream test project targets
  .NET 6 and resolves paths in a way that does not build on macOS. Core is unmodified, so upstream's
  behaviour *should* hold, but that is an assumption, not a measurement.
- Only the five supported games have been exercised at all, and only FireRed seriously.

## Reporting things

Please include your macOS version, which Mac (Apple Silicon or Intel), which core you were using,
and what the ROM is (game and whether it is patched). If the app crashed, there is usually a report
in `~/Library/Logs/DiagnosticReports/` - the top twenty lines are enough.

[PORT_STATUS.md](PORT_STATUS.md) documents what was ported, what was measured rather than assumed,
and the wrong conclusions reached along the way. If you are wondering whether something is a known
rough edge, it is the honest place to look.

## Credit

HexManiacAdvance is by [haven1433](https://github.com/haven1433/HexManiacAdvance) and is MIT
licensed, as is this fork. All the hard parts - the model, the table logic, the script engine - are
theirs.
