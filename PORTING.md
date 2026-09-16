# Porting plan

The rule for the whole port: **never change behavior in HexManiac.Core to suit the UI**.
If Core needs something from the platform, it goes through an interface (like `IFileSystem`),
and the Avalonia project implements it. That keeps the 2,500+ unit tests meaningful and lets
both UIs live in one repo.

## Phase 0: Core runs on macOS (setup.sh does this)

- Core builds with the installed SDK.
- Run `dotnet test src/HexManiac.Tests`. Tests needing ROMs look for a `sampleFiles` folder
  (see HMA's README). Failures that pass on Windows usually mean path separators (`\`),
  case-sensitive file names, or line endings in Core. Fix those in Core, since they're real bugs.

## Phase 1: First launch

Goal: open a ROM, see raw hex, click, scroll, type.

1. **CoreSeam.cs**: fix each error by finding what HexManiac.WPF does. Useful places:
   `MainWindow.xaml.cs` / `App.xaml.cs` (EditorViewModel construction),
   `Controls/HexContent.cs` (selection, scrolling, typing, resize).
2. **MacFileSystem.cs**: generate the missing `IFileSystem` members and implement each one
   by mirroring `WindowsFileSystem.cs` with the provided helpers.
3. `dotnet run --project src/HexManiac.Avalonia`, open a ROM, check that edits save and that
   the WPF build (on Windows) can still open the result.

## Phase 2: Hex view parity

- Cell text via Core's `ConvertCellToText` visitor, then a real `FormatDrawer` port:
  per-format colors from `Theme`, pointer underlines, anchor triangles, "Distraction Free" mode.
- Row headers (addresses / table entry names) and column headers
  (`HorizontalSlantedTextControl`).
- Remaining keys in `HexContent.cs`, right-click menus from `IContextItem`, autocomplete popup.
- Status bar, Goto / Find / Messages / Errors panels from `MainWindow.xaml`, start screen.
- If rendering gets slow, batch rows with `GlyphRun` like `GlyphCollector`.

## Phase 3: Tools tray (`TabView.xaml`)

In this order, since each is used more than the next:
Table tool → Text tool → Code tool (scripts, thumb) → Image tool (sprites, palettes) → Anchor editor.
Each is XAML bound to existing Core ViewModels, so this is mostly XAML translation.

## Phase 4: Other editors

Map editor, image editor, pokedex reorder, theme selector, quick edits/utilities.
Add a case to `TabContentTemplate` as each lands. Images: Core produces pixel data in HMA's own
format; convert into an Avalonia `WriteableBitmap` (`using var fb = bitmap.Lock()`).

## Phase 5: Mac polish and distribution

- Finder "Open With" and double-click via `IActivatableLifetime` / `FileActivatedEventArgs`.
- App icon (`.icns`), window restore, Settings under the app menu, `Meta` shortcuts everywhere.
- Universal build (arm64 + x64), Developer ID signing and notarization if you share it.

## WPF → Avalonia cheat sheet

| WPF | Avalonia |
|---|---|
| `FrameworkElement.OnRender(DrawingContext)` | `Control.Render(DrawingContext)` |
| `DependencyProperty` | `StyledProperty` / `DirectProperty` |
| `Visibility="Collapsed"` | `IsVisible="False"` |
| `Style.Triggers`, `DataTrigger` | Styles with selectors, `Classes`, pseudo-classes, bindings |
| `Dispatcher.Invoke` | `Dispatcher.UIThread.Invoke` |
| `OpenFileDialog` / `SaveFileDialog` | `TopLevel.StorageProvider` (async; see `UiSync`) |
| `Clipboard.GetText()` | `TopLevel.Clipboard.GetTextAsync()` |
| `MessageBox.Show` | Custom dialog window (`MacFileSystem.ShowDialog`) |
| `ContextMenu`, `Popup` | `ContextMenu` / `ContextFlyout`, `Popup`, `Flyout` |
| `GlyphRun`, `FormattedText` | Same names; slightly different constructors |
| `WriteableBitmap.WritePixels` | `WriteableBitmap.Lock()` then write to the framebuffer |
| `ModifierKeys.Control` | `KeyModifiers.Meta` on macOS (use `PlatformHotkeyConfiguration` for cross-platform) |
| `Menu` in window | `NativeMenu` → macOS menu bar |
| `IValueConverter` | Same interface, in `Avalonia.Data.Converters` |

## Working with Claude Code

From the repo root, a good first prompt:

> Read PORTING.md. Build src/HexManiac.Avalonia and fix the errors in CoreSeam.cs and
> MacFileSystem.cs by checking how src/HexManiac.WPF does the same thing. Don't change
> HexManiac.Core. Stop when it builds and tell me what you changed.

Then work one phase item at a time, and ask it to run `dotnet test src/HexManiac.Tests` after
any change that touches Core.
