# HexManiac.WPF → HexManiac.Avalonia port status

Every file in `src/HexManiac.WPF` (84 files, ~17,300 lines), in dependency order.
Rule: **no changes to HexManiac.Core.** If Core needs the platform, it goes behind an interface.

Legend: `[ ]` not started · `[~]` in progress · `[x]` ported & builds · `[L]` linked (portable source reused as-is)

---

## Tier 0 — Converters, helpers, no dependencies

- [x] `Resources/Extensions.cs` → `Resources/Extensions.cs` (Fluent/SetEvent helpers)
- [x] `Implementations/BooleanConverter.cs`
- [x] `Implementations/DoubleGridLengthConverter.cs`
- [x] `Implementations/ThemeConverter.cs`
- [x] `Controls/IntegerToHexConverter.cs`
- [x] `Controls/IntegerToBooleanViaMatchConverter.cs`
- [x] `Controls/MultiplyConverter.cs`
- [x] `Controls/TextStyleConverter.cs`
- [x] `Resources/IconConverter.cs`
- [x] `Resources/DesignerThemeResource.cs`
- [x] `Resources/MarkupExtensions.cs`
- [x] `Resources/MultiKeyGesture.cs` (WPF InputGesture → Avalonia KeyGesture chords)
- [L] `Resources/IndexedPng.cs` — linked from HexManiac.WPF (pure .NET)
- [L] `Resources/IndexedPngReader.cs` — linked
- [L] `Resources/IndexedPngWriter.cs` — linked
- [x] `Controls/DelayWorkTimer.cs` → `MacPlatform/DelayWorkTimer.cs`

## Tier 1 — Theme and shared resources

- [x] `Resources/Icons.xaml` (353 lines, vector icon dictionary)
- [x] `Resources/Icons.xaml.cs` — not needed: Avalonia has no Geometry.Freeze
- [x] `Resources/GeneralResources.xaml` (1370 lines: styles, control templates, data templates)

## Tier 2 — Primitive custom controls

- [x] `Controls/ColumnStackPanel.cs` (custom Panel: MeasureOverride/ArrangeOverride)
- [x] `Controls/SelectionRender.cs`
- [x] `Controls/TileImage.cs` (WriteableBitmap.WritePixels → Lock()/framebuffer)
- [x] `Controls/HorizontalSlantedTextControl.cs` (column headers, custom render)
- [x] `Controls/AngleBorder.xaml` + `.xaml.cs`
- [x] `Controls/AngleButton.xaml` + `.xaml.cs`
- [x] `Controls/AngleMenuItem.xaml` + `.xaml.cs`
- [x] `Controls/AngleTextBox.xaml` + `.xaml.cs`
- [x] `Controls/AngleComboBox.xaml` + `.xaml.cs`
- [x] `Controls/TextBoxLookAlike.xaml` + `.xaml.cs`
- [x] `Controls/EditableComboBox.xaml` + `.xaml.cs`
- [x] `Controls/Swatch.xaml` + `.xaml.cs`

## Tier 3 — Hex view (Phase 2 of PORTING.md)

- [x] `Implementations/FormatDrawer.cs` (491 lines; IDataFormatVisitor, GlyphRun batching) — + `Implementations/GlyphFont.cs` shim
- [x] `Controls/HexContent.cs` (763 lines; replaces the starter's stub `Hex/HexGrid.cs`)
- [x] `Controls/HexContentToolTip.xaml` + `.xaml.cs`
- [x] `Controls/AutocompleteOverlay.xaml` + `.xaml.cs`

## Tier 4 — Tools tray

- [x] `Controls/TableControl.cs`
- [x] `Controls/TableGroupPanel.cs` (1267 lines, custom layout)
- [x] `Controls/CommonTableStreamControl.xaml` + `.xaml.cs`
- [x] `Controls/WildPokemonControl.xaml` + `.xaml.cs`
- [x] `Controls/TextEditor.xaml` + `.xaml.cs`
- [x] `Controls/PythonPanel.xaml` + `.xaml.cs`
- [x] `Controls/PaletteControl.xaml` + `.xaml.cs`
- [x] `Controls/TutorialControl.xaml` + `.xaml.cs`

## Tier 5 — Editors

- [x] `Controls/ImageEditorView.xaml` (533) + `.xaml.cs` — registered in TabContentTemplate; tool buttons, zoom, pages, palette + mixer, canvas with SelectionRender/GridDecorator/TilePalettes
- [x] `Controls/SelectedBlockEditor.xaml` + `.xaml.cs`
- [x] `Controls/MapTab.xaml` (2043) + `.xaml.cs` (441) — code-behind ported in full; XAML covers the blocks panel, main map view, tile panel, border panel, tutorials, the full Events panel (object/warp/script/signpost/fly editors), the Header panel (blocksets, the 7-column Data Operations popup with its Import/Export File menus, shared-layout list, Z-connection editor, map fields, map scripts) and the full toolbar (Wild Pokemon popup, border editor, wave-function and event-template drag tiles, template settings popup, Map ID / Quick Goto)
- [x] `Controls/DexReorderView.xaml` + `.xaml.cs`
- [x] `Controls/StartScreen.xaml` + `.xaml.cs`

## Tier 6 — Shell, windows, dialogs

- [x] `Controls/TabView.xaml` (1254) + `.xaml.cs` (658) — full bottom bar (free space, address, length, selected bytes, update progress), sideways tool headers, anchor editor bar, and the whole tools tray: the table tool's 20 per-element DataTemplates, Show Uses, filter and Add-N-New; the text tool with Goto-Source menu, search key bindings and the drawn manual selection; the image tool in full; the code tool in both single and multi-content modes with the floating insert-var/insert-flag buttons; the log tool. Blinking cursor, scroll-position save/restore and the anchor selection round-trip are wired in code-behind
- [x] `Windows/MainWindow.xaml` (907) + `.xaml.cs` (827) — replaces starter stub; menu is a macOS NativeMenu (now 96 items, including the Developer menu). Tab drag-reorder, the goto blur + focus animation and the goto prefix-token grid are all in
- [x] `Windows/App.xaml` + `.xaml.cs` — resources, data templates, live theme wiring, the FileSystem / PaletteMixer / IsPaletteMixerExpanded window resources, argument parsing (path + `name:ADDRESS`, `--no-metadata`, `--dev-menu`, `--skip-splash`), single-instance forwarding over a named pipe, and the daily update check. The splash screen has no workable equivalent - see the note below
- [x] `Windows/AboutWindow.xaml` + `.xaml.cs`
- [x] `Windows/OptionDialog.xaml` + `.xaml.cs`
- [x] `Windows/RequestTextDialog.xaml` + `.xaml.cs`
- [x] `Windows/ThemeSelector.xaml` + `.xaml.cs`

## Tier 7 — Platform services

- [x] `Implementations/WindowsFileSystem.cs` → `MacPlatform/MacFileSystem.cs` (IFileSystem + IWorkDispatcher)

---

## Tier 4 in progress (tools tray)

`TableGroupPanel` (the 1267-line immediate-mode table renderer) is ported along with its
SpriteCache / RenderContext / every IGroupControl. Two things worth noting:
* ~390 of those 1267 lines are a commented-out WPF scratch block upstream; it is dead and was
  not reproduced.
* Avalonia **seals `Panel.Render`**, so controls that drew in WPF by overriding `Panel.OnRender`
  have to derive from `Control` instead (hit `TableControl`, which no XAML references anyway).

`Swatch` brought the one piece of genuinely Windows-only UI code found so far:
`DesktopColorPicker`, the palette eyedropper, is pure Win32 (gdi32 `BitBlt` + WinForms for the
cursor). Replaced by `MacPlatform/DesktopColorPicker.cs`, which reads the cursor from CoreGraphics
(`CGEventCreate`/`CGEventGetLocation`) and grabs the pixel with `/usr/sbin/screencapture`.
**Untested** -- it needs macOS Screen Recording permission, which I did not want to trigger
unprompted; it returns null rather than a wrong colour until granted.

Also added: `Resources/Icons.cs`, holding the 68 icon names as constants, because WPF generated an
`Icons` class from Icons.xaml that call sites used via `nameof(Icons.Check)`.

## Dialogs are now the real ports

`MacFileSystem.ShowOptions` / `RequestText` were hand-built placeholder dialogs; they now drive
the ported `OptionDialog` / `RequestTextDialog`, the same way WindowsFileSystem does.
Avalonia has no `DialogResult`: a modal Window closes *with* a value (`Close(result)`), so the
dialogs keep their WPF-shaped `Result` property and pass it to `Close`.

Other substitutions worth remembering:
* WPF `Hyperlink` inline -> no Avalonia equivalent; AboutWindow uses a flat styled Button.
* `TextBlock.FontSize` / `TextBlock.FontFamily` set on a container -> `TextElement.*` in Avalonia
  (a Grid has no FontSize of its own, so the WPF spelling fails to compile).
* WPF `ApplicationCommands` / routed commands (`NextPage`, `Select`) have no Avalonia counterpart;
  those call handlers directly.
* WPF shapes (`Path`, `Rectangle`, `Ellipse`) live in `Avalonia.Controls.Shapes`, not the media
  namespace.

## Application shell is up

`MainWindow` is ported and running: the WPF in-window `<Menu>` became a macOS `NativeMenu`
(86 items, every Ctrl accelerator reading as Cmd), plus the toolbar, Find / Hex-converter /
Message / Error panels, tab strip with per-tab context menu, and the Goto panel. Verified against
FireRed: the Goto shortcut tiles render real sprites decoded from the ROM, and closing Goto
reveals the hex editor behind it.

Three traps that each cost a crash-and-fix cycle:
* **`CommandParameter="False"` passes the string "False".** WPF wrote `<sys:Boolean>false</sys:Boolean>`;
  Avalonia needs `{x:False}` or the command's cast to bool throws at click time.
* **A `NativeMenuItem` is not a `StyledElement` and has no DataContext**, so `{hsv:MethodCommand X}`
  could not bind to it -- and `MethodCommandExtension` returning `this` (WPF's deferral trick)
  blew up as an InvalidCastException into the `ICommand` slot. It now returns null, and that menu
  item uses a Click handler.
* **Call the *generated* `InitializeComponent()`, not `AvaloniaXamlLoader.Load(this)`.** The
  generator emits both the `x:Name` fields and the method that assigns them; loading the XAML
  directly leaves every named field null. Also: do not name an element after its own type
  (`Name="TextBlock"` on a TextBlock) -- the generated field stays null.

## Second bug found the same way: the tray was never actually showing

The tools tray's `IsVisible` was **inverted** -- bound so the panel showed only when
`Tools.SelectedIndex == -1` (no tool selected). The column still reserved its width, so clicking
"Table" looked like it opened an empty panel; I had wrongly put that down to there being no table
at address 0. `EqualityToBooleanConverter` only does positive matches, so WPF's negative
DataTrigger needed a `NotEqualConverter`. With that fixed the Image tool renders its real
16-colour palette, gradient/merge buttons, Import/Export and "Open in Image Tab".

Worth noting for the rest of the port: **a reserved-but-blank panel is the signature of an
inverted IsVisible**, not of missing data.

## Tools tray is wired in

`TabView` now hosts the tray, so the already-ported tool controls are reachable: the sideways
Table/Text/Image/Code/Logs buttons, the resizable panel, and the bottom "Free Space" bar.
Verified against FireRed -- clicking Table opens the tray and the hex view reflows beside it.

* WPF stood those buttons on end with `LayoutTransform`. Avalonia has no LayoutTransform on
  arbitrary controls, only RenderTransform (which does not affect layout), so each button is
  wrapped in a `LayoutTransformControl`.

### Fixed: AngleTextBox / the Goto box

It was a real defect, and two separate ones:

1. **The binding was missing.** Porting MainWindow's markup I dropped
   `TextBinding="{Binding GotoViewModel.Text}"` and the Esc/Enter KeyBindings, so the box had
   nothing to bind to and no way to commit. (Avalonia does assign a `{Binding}` to an `IBinding`
   property rather than evaluating it, so the WPF spelling works once it is actually there.)
2. **The proxy swap was thrashing.** `AngleTextBox` swaps a TextBlock proxy for a real TextBox on
   hover -- WPF's trick to avoid paying for hundreds of TextBoxes in the tools tray. Replacing
   `Content` moves the visual out from under the cursor, so Avalonia immediately raises
   `PointerExited`, which swapped it back, which raised `PointerEntered`... 46 swaps at startup,
   and the TextBox never survived long enough to be clicked. WPF re-read `IsMouseOver` inside the
   handler and never saw this. Fix: activation direction is now passed in explicitly, and
   PointerExited only deactivates when the pointer is genuinely outside the control's bounds.

Verified end to end: typed `data.pokemon.names` into the Goto box, pressed Enter, and the view
navigated -- row headers showing BULBASAUR / IVYSAUR / VENUSAUR and the names PCS-decoded in the
cells. The same proxy backs the table filter and string-tool address boxes, so this fixes those too.

## The map editor runs

`MapTab`'s code-behind is ported in full and the editor renders PALLET TOWN from FireRed: the
blocks palette, the map with buildings/NPCs/water, movement-permission controls and the toolbar.
Its two biggest sub-panels (Events, Header) are now ported in full; only connection editing remains.

Two defects surfaced while wiring it up, both mine:

1. **New tabs never selected, and tab headers were unclickable.** `EditorViewModel.Add` sets
   `SelectedIndex` *before* raising CollectionChanged. WPF bound the TabControl straight to the
   editor (which is itself the collection), so the order worked out. The Avalonia port goes
   through the `EditorTabs` mirror, so the TabControl saw the new index while the item did not yet
   exist, coerced it away, and a TwoWay binding then fought every later change. Selection is now
   synced explicitly in both directions with a re-entry guard, and `EditorTabs` raises `Synced`
   so the view can re-apply the index after the mirror catches up.
2. **A collapsed panel kept painting.** The Tile/Border panels animate `Width` from 0, but their
   inner DockPanel is a fixed 400 wide; without `ClipToBounds` the content painted over the
   blocks panel at zero width. WPF clipped this implicitly.

## Everything except MapTab is ported

`StartScreen`, `DexReorderView`, `AutocompleteOverlay`, `SelectedBlockEditor` and
`TutorialControl` are done; only `MapTab` remains.

`TutorialControl` was the one flagged as un-translatable, and it needed two substitutions:
* **No imperative `BeginAnimation`.** WPF animated Canvas.Top/Left/Right/Opacity by constructing a
  DoubleAnimation per call. The Avalonia equivalent is to attach a `Transition` for that property
  once and then simply assign the value; `Resources/AnimationExtensions.cs` does that, mapping
  WPF's AccelerationRatio/DecelerationRatio onto easing curves (comparable motion, not identical).
* **No `ItemContainerGenerator`.** Container lookup is `ItemsControl.ContainerFromItem`.

Two more dead-code finds, both left out rather than reproduced:
* `SelectedBlockEditor` declares three Storyboards (AnimateToSplitView/BackView/FrontView) that
  nothing ever starts -- the only handler that would has its body commented out upstream.
* `StartScreen`'s GitHub pulse was a Loaded EventTrigger Storyboard; that one *is* live, and
  became a `Style.Animations` block.

### A porting trap worth knowing

`--` is illegal inside an XML comment. Several of these headers explain a WPF/Avalonia difference
using an em-dash-ish `--`, which makes the Avalonia XAML compiler fail with a bare AVLN1001 stack
trace and no line number. All ported .axaml files are now checked for it.

## Core Windows-only code found

(Recorded here as it turns up; each needs an interface in Core's UI-facing layer, implemented in Avalonia.)

- none found yet — `HexManiac.Core` builds clean on macOS and has needed no changes.

## Verified WPF -> Avalonia gaps (found while porting; apply to every remaining file)

| WPF | Avalonia | Notes |
|---|---|---|
| `GeometryCombineMode` | same name | the one WPF property name Avalonia kept |
| `FillRule="Nonzero"` | `"NonZero"` | capital Z, silently fails to parse otherwise |
| `x:Name` on resource + `FindName` | `x:Key` + `TryGetResource` | no FindName on resources; icons keyed `Icon.<Name>` |
| `DependencyProperty` | `StyledProperty` / `AttachedProperty` | change callbacks via `.Changed.AddClassHandler` |
| `FrameworkPropertyMetadata(AffectsMeasure)` | `AffectsMeasure<T>(...)` in static ctor | also AffectsArrange / AffectsRender |
| `OnRender(DrawingContext)` | `Render(DrawingContext)` | `PushTransform`/`Pop` become `using` scopes |
| `InternalChildren`, `RenderSize` | `Children`, `Bounds.Size` | |
| `DataContextChanged` with old/new | `EventArgs` only | must track the previous value by hand |
| `WriteableBitmap` Indexed8 + `BitmapPalette` | no indexed formats | expand to Bgra8888 when writing |
| `RenderOptions.SetBitmapScalingMode` | `SetBitmapInterpolationMode` | |
| `Application.Current.Resources.MergedDictionaries[0][name]` | `TryGetResource` | wrapped in `Resources/ThemeDictionary.cs` |
| Core's `Theme` inside a Control | **name collision** | Avalonia's `Control.Theme` shadows it -- alias `CoreTheme` |
| `KeyGesture` subclass for chords | `KeyGesture` is sealed | chords tracked in `Resources/MultiKeyGesture.cs`, driven from KeyDown |
| WPF `Popup.Reposition()` (private) | none | close + reopen the popup |
| `RotateTransform(angle, cx, cy)` | angle only | centre applied as translate x rotate x translate |
| `RoutedCommand` | none | `CommandExtension` unused in XAML, so dropped |

### Hex view verified working (Tier 3)

`FormatDrawer` + `HexContent` are ported and driving the real hex editor against FireRed:
per-format colours, pointer underlines, anchor triangles, PCS text decoding, the italic
free-space typeface, selection borders, row/column headers. Click-to-select, wheel scrolling and
the scroll bars were exercised directly and behave.

Notes from this pass:
* WPF's `GlyphTypeface` (CharacterToGlyphMap / AdvanceWidths / Height / Baseline, all em-relative)
  has no Avalonia counterpart. `Implementations/GlyphFont.cs` wraps `IGlyphTypeface` to restore
  that shape -- Avalonia reports advances in design units and a negative Ascent -- so
  FormatDrawer's layout arithmetic ports over unchanged.
* `GlyphRun` takes `GlyphInfo` records (`Avalonia.Media.TextFormatting`) instead of WPF's
  parallel glyph/advance/character lists.
* An Avalonia `KeyBinding` is **not** in the visual tree and has no DataContext, so WPF's
  `BindingOperations.SetBinding(keyBinding, CommandProperty, new Binding(path))` throws
  "Cannot find a DataContext to bind to". The path has to be anchored with an explicit Source.
* WPF's `DoubleAnimation` on HorizontalScrollValue becomes a `DoubleTransition` on the property.
* Mouse capture (`CaptureMouse`/`IsMouseCaptured`) becomes `e.Pointer.Capture` plus a tracked flag;
  `OnPreviewKeyDown` becomes a `RoutingStrategies.Tunnel` handler.

### Progress note

`GeneralResources.xaml` is ported and the app now renders with the real theme: FireRed loads,
the tab chrome and hex grid draw with Core's Theme brushes, no binding errors. Verified by
screenshot against `Pokemon - FireRed Version (USA, Europe).gba`.

Two extra files exist that WPF didn't have, both forced by Avalonia:
`Implementations/PixelBuffer.cs` (Core's 5r5g5b shorts -> Bgra8888, since Skia has no Bgr555 or
indexed formats) and `Resources/ThemeDictionary.cs` + `Resources/GestureTextConverter.cs`.

### The hard part still ahead

`GeneralResources.xaml` is **done**. Useful discovery while porting it: most of its
`MultiDataTrigger`s were dead code -- the ScrollBar/Slider/Thumb templates were copies of WPF's
defaults recoloured so that every trigger branch set the brush it already had. Those collapsed to
plain setters, so Avalonia's stock ScrollBar/Slider are restyled rather than re-templated.

Still genuinely hard, in rough order of cost:
`MapTab.xaml` (2043) + `.xaml.cs` (441), `TabView.xaml` (1254) + `.xaml.cs` (658),
`MainWindow.xaml` (907) + `.xaml.cs` (827), `TableGroupPanel.cs` (1267, custom layout),
`HexContent.cs` (763) + `FormatDrawer.cs` (491, GlyphRun batching), `ImageEditorView.xaml` (533).

## Starter-kit files replaced by real ports

- `Hex/HexGrid.cs` → superseded by `Controls/HexContent.cs` + `Implementations/FormatDrawer.cs`
- `Views/TabContentTemplate.cs` → superseded by `Controls/TabView.xaml` + `GeneralResources.xaml` templates
- `Views/MainWindow.axaml(.cs)` → superseded by the `Windows/MainWindow.xaml` port


## Late findings (worth keeping)

**`src/Directory.Build.props` builds `OutputPath` from `$(Configuration)`, which is empty when
Directory.Build.props is imported.** On Windows/VS the IDE always passes Configuration, so nobody
noticed; on the command line a plain `dotnet build` wrote to `artifacts/<proj>/bin/net8.0` while
`dotnet run` and the launcher read `artifacts/<proj>/bin/Debug/net8.0`. The result was a stale
binary that silently ignored every rebuild — it cost a long debugging detour. Fixed by defaulting
Configuration to Debug in that file before the paths are composed.

**A `ControlTheme` may not contain a child or descendent selector**, and Avalonia only finds out
when it *builds* the (lazily-evaluated) resource. `GeneralResources.axaml` had
`<Style Selector="Menu > MenuItem /template/ ...">` nested inside the MenuItem ControlTheme; it sat
harmless until the first control asked for the MenuItem theme, then threw
`InvalidOperationException` from inside `ContentPresenter.UpdateChild`. The visible symptom was a
completely blank tab with no exception surfacing anywhere. Top-level menu items now get their own
ControlTheme through `Menu.ItemContainerTheme`.

**A fault while switching tabs used to wedge the whole tab strip.** `SyncSelectionFromViewModel`
latched a re-entry guard around the `Tabs.SelectedIndex` assignment, and that assignment builds the
new tab's view inline — so any exception in that view left the guard set and killed every later tab
switch. It is a try/finally now, which is how the duplicate `x:Name="EditBorderButton"` in MapTab
turned from "tabs mysteriously stop working" into a one-line fix.

**Avalonia `Path` with `Stretch="Uniform"` reports the geometry's natural size**, where WPF's
reported zero. An unconstrained icon inside a button will therefore grow its whole row; icons need
an explicit Width/Height.

**`AvaloniaProperty.Register` field names matter to the XAML compiler.** `SearchByteProperty`
backing a `SearchBytes` property compiled fine but could not be bound from XAML
(`AVLN3000: Unable to find suitable setter or adder`); the field must be `<PropertyName>Property`.


**Single instance had to change mechanism, not behaviour.** WPF gates on a named `Mutex` and only
then opens a named pipe. On macOS the two processes here - one launched by launchd from the .app
bundle, one from a shell - both reported `createdNew == true` for the same mutex name, so the mutex
never saw the running copy. The pipe alone is sufficient and is what carries the message anyway:
try to connect, and if that succeeds hand over the file and exit. The pipe name also had to be
hashed: a named pipe on Unix is a socket at `/tmp/CoreFxPipe_<name>`, and `sockaddr_un.sun_path`
caps the path at 104 bytes, which WPF's "{HexManiacAdvance} : <assembly location>" exceeds.

**No splash screen.** WPF shows a `System.Windows.SplashScreen` (a Win32 layered window the loader
draws before the CLR UI exists). An Avalonia window created during
`OnFrameworkInitializationCompleted` is built before the macOS main loop runs, and the native
window it creates then outlives `Close()` - an empty 300x180 frame stays on screen for the life of
the process. `Opened`, `Hide()`-then-`Close()`, and a posted Background close all behave the same.
A ghost window is worse than no splash, so there is none; `--skip-splash` is still parsed so
Windows command lines transfer unchanged.

**Watch out for the shape of Avalonia's build errors.** They are reported as
`file : Avalonia error AVLN####:`, not `file: error AVLN####:`. Grepping build output for
`": error"` therefore misses them entirely, and the build "passes" while producing no new binary -
which is exactly what happened here for several edits in a row. Grep case-insensitively for
`error`, or check the output binary's timestamp.


**A `ControlTheme` is looked up by the control's own type, with no fall-back to its base.** A
derived control such as `AngleMenuItem` therefore gets *no* theme unless one is declared for it -
it renders as a bare box. WPF's equivalent style keyed off `MenuItem.Role == TopLevelHeader`, which
has no Avalonia counterpart, so the theme is now declared for `AngleMenuItem` directly.

**`Menu.ItemContainerTheme` overrides a derived item's own theme.** Setting it to style top-level
menu items silently replaced `AngleMenuItem`'s theme wherever one was declared inside a `<Menu>`.
The top-level tweaks live in `Application.Styles` instead, where a `Menu > MenuItem` descendant
selector is legal and nothing is forced onto the containers.


## Round 3: eyedropper, map editor, and the last named gaps

**Eyedropper (`MacPlatform/DesktopColorPicker.cs`) is no longer a guess.** It was shelling out to
`/usr/sbin/screencapture -R<x>,<y>,1,1` per pick, which writes a temp file, spawns a process, and
fails outright on this OS ("could not create image from rect"). It is now CoreGraphics in-process -
`CGEventCreate`/`CGEventGetLocation` for the cursor (WPF: `Control.MousePosition`) and
`CGDisplayCreateImageForRect` for the pixel (WPF: `BitBlt` from the desktop DC). The captured
CGImage is redrawn into a 1x1 RGBA context with `kCGInterpolationNone`, which pins the byte order
and picks the exact device pixel out of the 2x2 image a one-point rect returns on a Retina display.
Verified against a known region of the app's own window: the colour read through the 1x1 path
matches the same pixel read from a 16x16 grab.

The arming was also wrong: WPF called `CaptureMouse()` so the *next click anywhere* committed the
colour, while the port only listened on the PaletteControl itself - so clicking the thing you
wanted to sample never committed. The handler now goes on the TopLevel. Clicking another
application's window still cannot be intercepted without a global event tap (Accessibility
permission), so the grab stays armed until the user clicks back inside this app.

**The map was drawing in the bottom-right corner instead of centred.** WPF set
`Canvas.Top`/`Canvas.Left`/`ZIndex` through an `ItemContainerStyle`, and those have to stay on the
*container*: a Canvas positions the generated `ContentPresenter`, so the same attached properties
written inside an `ItemTemplate` are simply ignored and every map lands on the canvas origin.
Each `BlockMapViewModel` positions itself at `(-PixelWidth/2, -PixelHeight/2)` and relies on those
offsets reaching the container. `TutorialControl` had the identical bug - all 36 cards stacked at
y=0, so only one was ever visible.

**Map connection editing is in.** `MapButtons` was an empty `<Grid/>` placeholder; it is now the
real `ItemsControl` over `MapEditorViewModel.MapButtons`, with the shifter arrows, their tooltips
and context menus. WPF chose `Canvas.Left` vs `Canvas.Right` (and Top vs Bottom) with four
DataTriggers on `AnchorLeftEdge`/`AnchorTopEdge`; an Avalonia setter cannot be swapped by a
trigger, so all four are set and `EdgeAnchorConverter` returns NaN - which Canvas reads as
"not set" - for the two that do not apply. It is a MultiBinding over the position *and* the flag so
the shifter still tracks while it is dragged. The highlight cursor and the hover/zoom readout that
sit in the same layer came across with it.

**Shared icon geometries were a latent crash.** Avalonia's `Shape` applies its `Stretch` by
assigning `Transform` to the Geometry it is handed - mutating the shared resource - and the next
`Path` that wants the same icon throws
`Unable to cast TransformedGeometryImpl to IStreamGeometryImpl` from `Clone()`. Two connection
shifters wanting the same arrow took the whole app down. `IconExtension.GetIcon` now hands out a
private copy.

Also closed: the trainer team editor (the text editor with the party sprites behind it at half
opacity, plus its autocomplete overlay) and the mart shop autocomplete, both of which had been
reduced to read-only lists; the "hold space to see the maps underneath" reveal and the
block-selection pop, which WPF drove with Storyboards from DataTriggers and which are eased from
code-behind here because Avalonia has neither DataTriggers nor an Animatable `GradientStop`; the
code tool's inline help popup; and the Python automation panel, which is fully wired in Core and
whose control was ported, but which MainWindow never actually hosted.


## Round 4: 1:1 sweep

Three mechanical checks, each of which found real defects that a build cannot catch:

1. **Every `x:Name` in the WPF file vs the port.** This is what surfaced the missing Python
   automation panel (fully wired in Core, control ported, never hosted), the code tool's inline
   help popup, and the map connection shifters.
2. **Every binding path and `MethodCommand` name against the public members of HexManiac.Core.**
   Bindings to properties that do not exist fail silently unless the panel happens to be realised
   while the log is being watched. This caught `HasBasicFlag` / `HasBasicItem` / `HasBasicText` /
   `BasicFlagText` / `BasicItem` - five invented names standing in for the real
   `ShowBerryContent` / `ShowItemContents` / `ShowNpcText` / `BerryText` / `ItemOptions`, so the
   whole per-kind NPC editor block was dead markup - plus `MoveTypeOptions` (the real one is the
   `FacingOptions` FilteringComboOptions, which is why Movement Type was always blank) and
   `BeforeTextEditor` / `WinTextEditor` / `AfterTextEditor` (the real names are prefixed
   `Trainer...`). The sweep now reports clean.
3. **Per-file counts of each custom control type.** This is what found the event editors that had
   been reduced or dropped: the Tutor block (4 text editors), the Trade block (5), the Legendary
   block (species/level/hold item/capture flag, the cry editor and the sample clear script), the
   Rematch-trainer block with its per-team TabControl, the "elaborate script" fallback panel, the
   Clone checkbox, and the generate-an-unused-flag button.

The image editor also regained its per-tool option rows - cursor size (1/2/4/8), tile-palette mode,
and the flip buttons, each shown for the tool it belongs to - and the block preview.

### Still deliberately different

* **No splash screen** and **single instance over the pipe rather than a mutex** - see the notes
  above; both are platform limits, not omissions.
* **No scroll animation on the hex view.** WPF snapshots the old content into a RenderTargetBitmap
  and slides it under the new one with a DoubleAnimation. `AnimateScroll` is still a property here
  and the plumbing is in place, but the animation itself is not implemented.
* **Rich tooltips are plain text in a few places.** WPF composes several tooltips out of a DockPanel
  with an icon plus italic body text; where the port collapsed those to a string the wording is
  preserved but the layout is not.
* **The eyedropper cannot sample another application's window** without a global event tap.


## Round 5: flicker and block picking

**Clicking a block in the tileset panel selected a different one.** The pointer handlers were
attached to the `BlockViewer` ScrollViewer rather than to the `PixelImage` inside it. Those
handlers turn a position into a block index by dividing by 16, and a position measured against the
scroll *viewport* is short by the scroll offset - so the error was zero at the top of the list and
grew as you scrolled. WPF hangs them on the image; so does the port now. Verified: clicking 16px
apart moves the reported Block ID by exactly one.

**Flicker.** Worth recording what the measurements actually showed, because two of my own
instruments lied:

* A pixel-diff harness (capture a small screen region repeatedly, hash it) reported the map and
  tileset panel changing on nearly every frame. It reports the same "changes" with the renderer
  provably frozen, so it is measuring capture noise over image content, not repaints. Flat UI
  chrome hashes stably, which is what made it look credible at first.
* Avalonia's own `RendererDiagnostics.DebugOverlays` (Fps + DirtyRects) is the instrument that
  actually answers the question. At rest the frame counter does not advance at all: **0 fps, no
  dirty rects**. There is no idle repaint loop.

So the flicker is interaction-time, and four things were causing repaints per pointer-move:

1. **Tooltips recreated a popup window per cell.** Both `MapTab.UpdateTooltipContent` and
   `HexContent.MakeNewToolTip` followed WPF in building a brand new `HexContentToolTip` each time
   ("to prevent a glitch of text changing as the old one fades to closed"). That is a WPF fade
   artifact; in Avalonia replacing the tip tears down and recreates a popup *window*, and the map
   editor does it for every cell the pointer crosses. The control is reused now and only its
   DataContext is repointed - and the map's version skips the work entirely when the content has
   not changed.
2. **`HexContent.ClearTooltip` repainted the whole hex grid** on every pointer move over unformatted
   bytes. The tooltip is a popup with its own surface, so the repaint bought nothing; it is now a
   no-op when there is nothing to clear.
3. **`PixelImage.UpdateSource` assigned `Width`/`Height` unconditionally**, re-running layout on
   every pixel update. WPF used a LayoutTransform and never touched them here. Only assigned on
   change now.
4. **`PixelImage` wrote new pixels into the bitmap the compositor was displaying.** Avalonia's
   render thread reads the `WriteableBitmap` currently assigned to `Source`, so writing into that
   same instance tears. It now alternates two buffers and swaps, and an update generation counter
   stops a slow async update (only map bitmaps take the async path) from publishing over a newer
   one.

One real bug fell out of that last area: `UpdateSource` cleared `OpacityMask` unconditionally,
which destroyed the map template's own RadialGradientBrush - so the hold-space reveal never worked
- and raised a property change on every pixel update. WPF guards with `OpacityMask is VisualBrush`
so that it only clears a mask it created; the port now does the same with `ImageBrush`.

## Round 6 - tile selector, blocks-panel header, and a data-corruption bug

### Tile/block picker offset (user: "tile selector is still off")

WPF scaled these pixel views with `LayoutTransform`, which leaves the element's *own* coordinate
space unscaled - so `e.GetPosition(image)` came back in source pixels and WPF could divide by 16
directly. Avalonia has no LayoutTransform, so `PixelImage` bakes `SpriteScale` into `Width`/
`Height`; positions therefore arrive already multiplied by the scale (2x for the blocks canvas,
3x for the tile render). `SourcePixelPosition` divides it back out, and every Blocks/Tiles/Border
handler in `MapTab.axaml.cs` plus `OffsetRender*` in `TabView.axaml.cs` now goes through it.
`ImageEditorView`'s `EditImage` gets `ScaleAffectsLayout="False"`, mirroring WPF's
`LayoutTransform="{x:Null}"` on that one control.

Verified: two clicks 16px apart inside one 32px cell select the same block; 32px apart advances
exactly one; the highlight lands under the cursor and the preview / Block ID / Movement agree.

### Blocks-panel header labels overlapped

Two causes, both mine:

1. The header Grid's `RowDefinitions="Auto,75"` shorthand cannot express WPF's
   `MinHeight="112"` on row 0, so the row collapsed onto the block preview. Restored as explicit
   `<RowDefinition>` elements.
2. **Avalonia's Fluent theme defaults to FontSize 14; WPF defaults to 12**, and HexManiac.WPF
   never overrides it - so every hard-coded Width/Height/Margin in the ported XAML had been laid
   out against 12pt text while rendering at 14pt. `App.axaml` now sets
   `ControlContentThemeFontSize` 12 and a `Window { FontSize: 12 }` style (templated controls take
   the resource, bare TextBlocks take the inherited value). This is a port-wide fidelity fix, not
   just a blocks-panel one.

While diffing that panel against WPF, four other differences were restored: the Cancel button and
the MultiTileDrawRender decorator were visible on `BlockEditorVisible` alone where WPF used two and
three DataTriggers respectively (new `AnyTrueConverter` combines them), the bag and
auto-permission tooltips had been flattened to one line, and the "Usage Count:" Runs had lost the
space WPF got from the newline between them.

### Tab switching silently corrupted the ROM

Reproduction: open a map, switch to the hex tab, switch back. Both tabs show the unsaved-change
marker and the map tab is renamed `(unnamed).3-0`.

Cause: switching tabs unloads the outgoing tab's controls, which detaches their `ItemsSource`
bindings. An Avalonia `SelectingItemsControl` with no items coerces `SelectedIndex` to -1
(`SelectedItem` to null), and both properties bind **TwoWay by default**, so the coerced value is
written straight back to the view model. These selections are ROM fields:
`BlockMapViewModel.SelectedNameIndex = -1` stores `regionSectionID = -1 + 0x58 = 0x57` on FireRed,
which is an empty name - hence `(unnamed)`. WPF never produced this write, so no HexManiac.Core
setter defends against it, and Core must not be modified.

Fix: `IgnoreEmptySelectionConverter` returns `BindingOperations.DoNothing` from `ConvertBack` for
null and for negative ints, cancelling that one write while leaving real user selections alone.
Applied to all 31 `SelectedIndex`/`SelectedItem` bindings across MapTab, TabView,
SelectedBlockEditor, ImageEditorView and OptionDialog.

Verified: repeated map <-> hex round trips, including with the Map Header panel open (every
combo in it torn down each time), leave no unsaved-change marker and every header field intact.

### Round 6b - missing MainWindow wiring found by diffing method names

Listing every handler/method in each WPF `.xaml.cs` and checking which names never appear in the
matching port file turned up several genuine gaps (the rest of the list is WPF-only plumbing -
`BitBlt`, `OnRenderSizeChanged`, `Mouse*` naming). Ported this round:

* **Focus routing.** `FocusGotoBox`, `ResetFocus`, `FocusPrimaryContent` and the
  `MoveFocusToFind` / `MoveFocusToHexConverter` / `MoveFocusToPrimaryContent` /
  `GotoViewModel.MoveFocusToGoto` subscriptions did not exist. The visible symptom: opening the
  goto panel left the keyboard nowhere, so you had to click the box before you could type - and
  the app opens straight into that panel. `FocusGotoBox` defers to Background priority because
  GotoViewModel raises the event while the panel is still collapsed, and asks the AngleTextBox for
  a real TextBox first (it only swaps one in when hovered or focused). `Window.Activated` covers
  WPF's `OnActivated`; `OnGotFocus` would have re-stolen focus on every click inside the panel.
* **Deferred work.** `EditorViewModel.RequestDelayedWork` was unsubscribed, so work Core queues
  from inside a change notification never ran. WPF flushed the queue from `PreviewMouseDown`; the
  port uses a tunnelling `PointerPressed` handler for the same ordering.
* **`EditBoxVisibilityChanged`.** Closing the find or hex-converter box now also clears
  `GotoViewModel.ShowAutoCompleteOptions`, so the autocomplete list cannot outlive its box.
* **Crash reporting.** `HandleException` / `AppendGeneralAppInfo` / `AppendException` /
  `ExtractExceptionInfo` are ported to `Windows/CrashReporter.cs`, and the `CustomTraceListener`
  that turns `Debug.Assert` into an IFileSystem options dialog to `Windows/CustomTraceListener.cs`.
  Avalonia splits WPF's single `DispatcherUnhandledException` in two, so `App.axaml.cs` hooks both
  `Dispatcher.UIThread.UnhandledException` (marked Handled, as WPF did) and
  `AppDomain.CurrentDomain.UnhandledException` for off-thread faults.
  *Documented as different:* WPF offered to switch to software rendering when the stack contained
  `SyncFlush`. Avalonia fixes the render mode at platform init, so there is no equivalent toggle
  and a render failure falls through to the normal report.

`AngleComboBox` creates its own TwoWay SelectedIndex binding in code (for FilteringComboOptions),
which the XAML sweep could not reach, so it gets the same converter explicitly. Core's
`FilteringComboOptions.SelectedIndex` setter happens to reject the write already, but relying on
that would leave the port one Core refactor away from the same corruption.

Regression test for the whole class of bug: open `data.pokemon.stats` in the table tool (type1,
type2, genderratio, growthrate, egg1, item1, item2 are all selection-bound to ROM fields), open a
map in a second tab, and round-trip between them. No unsaved-change marker, every field intact.

## Round 7 - the goto prefix tokens flickered on hover

User report: "when i hover over the data and script boxes it flickers constantly."

Measured it rather than guessing, by subscribing to `ToolTip.IsOpenProperty.Changed` and logging
every transition. With the pointer resting on the "data" token: **42 open/close transitions in
0.75 s (~28 Hz), continuously**. Pointer anywhere else: zero. Two separate causes.

### 1. A tooltip that was empty instead of absent

WPF put a `HexContentToolTip` in a Setter and *cleared it* with
`DataTrigger Binding="{Binding HoverTip}" Value="{x:Null}"`, so a token with no hover tip had no
tooltip at all. The port attached one unconditionally and tried to hide it with
`IsVisible="{Binding HoverTip, Converter=IsNotNull}"` - which was doubly wrong: that binding
resolves against the tooltip's own DataContext, which *is* HoverTip, so it read `HoverTip.HoverTip`
and was always false. So every token had a tooltip, and every tooltip was empty.

Avalonia still opens a popup for an empty tooltip. A zero-size popup lands on the pointer, takes
the hover off the button underneath, closes because the button is no longer hovered, and the button
takes the hover straight back - at `ShowDelay="0"`, forever. That is the constant flicker.

`GotoHoverTipConverter` now returns the `HexContentToolTip` only when `HoverTip` has content and
`null` otherwise, so `ToolTip.Tip` is genuinely unset - what WPF's cleared Setter did.

### 2. Pointer placement re-shows the tooltip on every mouse move

Tokens that *do* have a tip still blinked, in short bursts that stopped the moment the pointer
stopped. Avalonia's default `PlacementMode.Pointer` makes the tooltip follow the cursor, and it
repositions by closing and reopening the popup. WPF's Placement=Mouse tooltip is placed once when
it opens and then stays put, so this never happened there.

`ToolTip.Placement="Bottom"` anchors the tip to the token instead. After the change the tooltip
opens once and stays open: **1 transition, then silence**, versus 8-per-sweep before.

Only the goto tokens get this; it is deliberately not applied app-wide. `HexContent` and `MapTab`
own tooltips that describe whatever is under the cursor and *should* track it, and anchoring a tip
to the bottom edge of a control that fills the window would put it in the wrong place entirely.
Measured for comparison: dragging across the hex view produces 2 open/close pairs for two passes -
the tip opening over formatted data and closing when it leaves, which is correct behaviour.

### Incidental: the crash reporter fired for real

While testing, an automation-driven click on a *disabled* toolbar button made Avalonia throw
`ElementNotEnabledException` from `AvnAutomationPeer.InvokeProvider_Invoke`. `crash.log` was
written correctly, with tab inventory and recent log lines - the Round 6b reporter works end to
end. The process still exited, because that exception is raised inside the native automation
callback rather than inside the dispatcher's try block, so it reaches
`AppDomain.UnhandledException` (which cannot cancel termination) instead of
`Dispatcher.UIThread.UnhandledException`. It is an Avalonia framework path, only reachable by an
accessibility client pressing a disabled control - not by mouse or keyboard - and it is not
something the port can fix without replacing every Button's automation peer.

## Round 8 - an emulator inside the editor

New feature, with no WPF counterpart to port from. HexManiac.WPF only ever shelled the *saved*
file out to whatever the OS runs `.gba` with (`EditorViewModel.RunFile`, F5). This runs the ROM
**as it stands in the editor**, unsaved edits included, in a window beside it.

### Why a libretro core rather than a from-scratch emulator

Writing an accurate GBA emulator is a project in its own right, and a rough one would make the
editor lie about what the ROM does. Instead the editor borrows a core the user already has.
RetroArch ships its cores as frameworks inside its app bundle, and the one on this machine is
mGBA 0.11-dev, universal arm64 + x86_64, exporting the complete libretro C ABI.

Three things were checked with a standalone C probe before a line of C# was written
(`scratchpad/retroprobe/`), because each one could have sunk the approach:

* `dlopen` of a framework signed by another app works from a .NET process - no library-validation
  refusal.
* `retro_get_system_info().need_fullpath` is **false**, so the core accepts a ROM as a pointer and
  a length. This is the whole feature: no temp file, no save, the bytes in `IDataModel.RawData` are
  what boots.
* The core loaded the 16MB FireRed image and produced 240x160 RGB565 frames at pitch 512,
  59.7275fps, 65536Hz audio.

### What was added (all under `src/HexManiac.Avalonia/Emulation/`, nothing in Core)

| File | Role |
| --- | --- |
| `Libretro.cs` | The libretro C ABI - structs, delegates, environment commands. |
| `LibretroCore.cs` | Loads a core, answers its environment callbacks, converts frames to BGRA, exposes run/reset/serialize/input. |
| `EmulatorPaths.cs` | Finds a core (RetroArch's frameworks, a local `cores/` folder, or one the user picks) and owns the BIOS/save directories. |
| `EmulatorAudio.cs` | macOS AudioQueue output - a ring buffer filled by the emulation thread and drained by the device thread. |
| `EmulatorSession.cs` | The emulation thread, frame pacing, and the latest-frame hand-off. |
| `EmulatorScreen.cs` | Draws frames, nearest-neighbour, aspect-correct, optional whole-pixel scaling. |
| `Windows/EmulatorWindow.axaml(.cs)` | The window: toolbar, status, keyboard mapping, no-core guidance. |

`MainWindow` gets a toolbar button and **File > Test in Emulator (F6)** - deliberately a different
key from F5, because F5 runs the file on disk and F6 runs what you are looking at.

Design notes worth keeping:

* **A separate window, not a tab.** The tab list belongs to HexManiac.Core, and Core is not
  modified by this port. `Show()` rather than `Show(owner)`, so it can sit beside the editor or on
  another display instead of being pinned above it.
* **The ROM is fetched through a callback**, not captured once. HexManiac replaces
  `BaseModel.RawData` wholesale when the ROM is expanded, so a captured array would silently go
  stale; re-reading it on every boot also means "Reload ROM" always sees the current edits.
* **The core gets a copy**, not the model's array. The libretro contract says the core must take
  its own copy, but pinning a live model array across a native call is not worth the risk - and a
  copy means the running game is a snapshot rather than something that mutates under itself while
  you type.
* **Threading**: the emulation thread is the only thread that touches the core. The UI reaches it
  through atomic button bits, one command flag per frame, and a frame copy under a lock held for
  exactly one `Array.Copy`. Repaints are driven by the compositor via `RequestAnimationFrame`.
* **Audio is optional.** If AudioQueue fails the emulator runs silently and the status bar says
  `(no audio)` rather than refusing to start.
* Battery saves go to `~/Library/Application Support/HexManiacAdvance/Emulator/saves`, never next
  to the user's ROM.

### Verified

Running against FireRed: the core loads, the game boots and animates at a measured 59.7fps, audio
initializes (no `(no audio)` in the status), Pause/Resume, Reset, Reload ROM and Save/Load State
all behave - Save State then Reset then Load State returns to the exact saved frame.

That an edit to the in-memory buffer actually reaches the running game was measured separately
(`scratchpad/retroprobe/reload.c`), with a control for nondeterminism:

```
boot 1, unmodified buffer    -> 6ad58dc5
boot 2, same buffer again    -> 6ad58dc5   SAME (control: emulation is deterministic)
edit 1MB at 0x400000         -> 50a559c5   CHANGED the picture
after undoing every edit     -> 6ad58dc5   back to the original picture
```

### Keyboard input - two bugs, both found by the user reporting "keyboard does not work"

**1. The keys never reached the handler.** `EmulatorScreen` derives from `Control`, which is not
focusable, so `Screen.Focus()` silently did nothing and the keyboard stayed on whichever toolbar
button had it. `KeyDown` *bubbles*, so by the time it reached the Window every child had already
had its turn - a focused Button consumes Enter and Space as "click me", and Avalonia's keyboard
navigation consumes the arrow keys to move focus. Both mark the event handled, so the Window's
`OnKeyDown` override saw nothing. That is six of the ten buttons a GBA has.

Fixed by making the screen focusable (and focusing it on Opened and on click), and by taking the
input on the **tunnelling** route instead of an override - tunnelling runs root-first, so the game
gets the keys before any child can claim them. MainWindow already uses this pattern for its chord
shortcuts.

**2. A tap shorter than one frame was dropped.** The core samples input once per `retro_run`
(`retro_input_poll`), so a press that began and ended between two frames was never seen.
`LibretroCore` now latches: `pressedSincePoll` accumulates every button that went down since the
last poll, and the poll presents `held | tapped` for exactly one frame. A human holding a key for
~100ms was never affected, but scripted or auto-repeating input was, and the latch costs nothing.

Verified after the fix by driving FireRed from the keyboard alone: Start skipped the Charizard
intro to the title screen, then title screen -> main menu -> NEW GAME -> the in-game CONTROLS
tutorial, all on Return.

### Requires a core

HexManiacAdvance ships no emulator; mGBA is MPL-2.0 and separately distributed, so the editor
loads one the user already has rather than redistributing it. With no core found, the window
explains that and offers to browse for one.

## Round 9 - an MCP server, so an assistant can work on the open ROM

New feature, no WPF counterpart. A Model Context Protocol server inside the editor lets a client
such as Claude Code read and edit **the ROM that is open in HexManiacAdvance**, unsaved changes
included, and drive the emulator to check its own work.

Off by default: **Tools > Claude Assistant (MCP Server)**, or the new `--mcp` launch argument to
come up already listening. It is a local listener with write access to the user's ROM, so it is
never something they did not turn on.

### Shape

`Mcp/McpServer.cs` speaks the Streamable HTTP transport on `HttpListener` + `System.Text.Json` -
no SDK dependency, because a tools-only server needs very little of the protocol (`initialize`,
`tools/list`, `tools/call`, `ping`, plus JSON-RPC batching and notifications). It binds 127.0.0.1
only and rejects any request whose `Origin` is not loopback, which is what the spec asks for to
stop a web page driving a local server through DNS rebinding.

`Mcp/HexManiacTools.cs` has the 14 tools, every one of them hopping to the UI thread first because
HexManiac.Core assumes a single thread:

* `rom_status`, `rom_goto`, `rom_read_bytes`, `rom_write_bytes`, `rom_search`, `list_tables`
* `table_read`, `table_write` - through the model, so pointers, string encoding and field widths
  stay correct; the tool descriptions say to prefer these over raw bytes
* `rom_undo`, `rom_redo`
* `run_python` - HexManiac's own IronPython automation engine, the escape hatch
* `emulator_boot`, `emulator_screenshot`, `emulator_input` - the feature that makes this more than
  a file editor: make a change, boot it, and *look* at the result

Two deliberate limits: there is **no save tool** (writing the .gba stays the user's decision), and
every write is sealed into the undo history so the user can back out anything an assistant did.

### Three things that had to be got right

1. **Edits were unreachable by undo.** HexManiac accumulates changes in
   `ChangeHistory.CurrentChange` and only pushes them onto the undo stack when something calls
   `ChangeCompleted()` - which the editor does at the end of a user gesture. A tool call is the
   equivalent gesture, so each mutating tool now calls it; without that an assistant's edits sat in
   an uncommitted token that Undo could never reach.
2. **`EditorViewModel.Undo` silently does nothing while the goto panel is open.** It is built with
   `CreateWrapperForSelected(..., preventIfScreenBlocked: true)`, and that check lives in `Execute`
   while `CanExecute` still returns true - so undo reported success and changed nothing. Since the
   app opens *on* the goto panel, that was every undo. Fixed by going through the selected tab's
   own `Undo`/`Redo`, which have no screen guard: an MCP client is not looking at the screen. (Not
   a port bug - upstream behaviour that only shows up when something drives the editor headlessly.)
3. **`print()` in the Python engine opens a modal dialog**, which would hang the tool call and trap
   the user behind a box they did not ask for. `run_python` swaps in a collector for the duration
   and returns what was printed.

### Verified end to end over HTTP

`initialize` handshake; `tools/list` returns all 14; `rom_status` reports the open FireRed
(`BPRE0`, 16MB); `table_read data.pokemon.stats[1]` returns Bulbasaur's real stats (45/49/49/45/
65/65); `table_write` changes hp 45 -> 99 and the editor shows unsaved changes; `run_python`
renames it and reads both back; `emulator_boot` + `emulator_screenshot` return a real 240x160 PNG
of the running game; `emulator_input "start"` skips the intro to the title screen, confirmed by the
next screenshot. Undo/redo round-trips three mixed edits (two table writes and a Python rename)
back to `BULBASAUR 45/49` with the tab reporting no unsaved changes, and the .gba on disk was never
touched.

### Connecting a client

With the server running:

    claude mcp add --transport http hexmaniac http://127.0.0.1:8261/mcp

**Tools > Show MCP Setup Command** prints that line with the actual port and offers to copy it.
The port walks upward from 8261 if it is taken, so a second copy of the editor gets its own.

## Round 10 - a chat panel inside the editor

The MCP server lets an outside client work on the ROM. This is the other half: a chat dock inside
HexManiacAdvance, so the assistant is where the work is.

**Toolbar speech-bubble button, Tools > Toggle Claude Chat, or Cmd+Shift+C.** It docks on the right
in the same grid as the Python automation panel (`TabContainer` grew from `*,0,0` to `*,0,0,0,0`;
widths are set in code because Avalonia has no trigger that can drive a ColumnDefinition).

### It reuses the MCP tool registry rather than the MCP server

`ChatSession` takes the same `IReadOnlyList<McpTool>` that `McpServer` publishes and calls it
**directly, in process** - no HTTP hop, no second definition of a tool. So the panel and an
external client such as Claude Code have exactly the same 14 abilities, and adding a tool adds it
to both. `emulator_screenshot` returns its PNG as an image block in the `tool_result`, so the model
in the panel can actually look at the running game.

New files: `Ai/AnthropicClient.cs` (a hand-written Messages API client - no SDK, same reasoning as
the MCP server), `Ai/ChatSession.cs` (the tool loop), `Ai/ChatRoleConverter.cs`,
`Controls/ChatPanel.axaml(.cs)`.

### About the API key

Read from `ANTHROPIC_API_KEY`, or from `~/Library/Application Support/HexManiacAdvance/
anthropic-api-key.txt` if the user chooses to put it there. **The editor only ever reads it** - it
is never written anywhere, never logged, and there is no dialog asking the user to type it in.
Without a key the panel shows exactly where to put one and disables Send.
`ANTHROPIC_BASE_URL` redirects the client at a gateway or proxy, the same variable Anthropic's own
tools honour.

### The system prompt earns its keep

It tells the model to look before it writes, to prefer `table_read`/`table_write`/`run_python` over
raw bytes (they go through the model, so pointers and text encoding stay correct), to use
`rom_goto` so the editor's view follows the conversation, to check its own work with
`emulator_boot` + `emulator_screenshot`, and that saving is the user's decision - there is no save
tool and it should not offer one.

### Verified

Against a stand-in Messages API on localhost (so no live key was involved), driving the real
editor with FireRed open: the panel offered all **14 tools**, the model's `table_read` call
executed against the open ROM, the 451-character result went back as a `tool_result`, and the
transcript rendered user bubble, assistant text, a dimmed monospace tool line
(`table_read (table: data.pokemon.stats, index: 1)`) and the final answer quoting `hp = 45` -
Bulbasaur's real HP, read through the editor's own model. The no-key path shows the setup notice
and disables Send.

**Not verified:** a live call to the real API, which needs the user's own key.

Known limitation: responses are not streamed, so a long answer appears all at once after a
"Working..." pause. The Messages API's SSE transport would fix that; it needs incremental
`input_json_delta` accumulation for tool calls, which is a bigger piece of work than the rest of
the panel put together.

## Round 10b - the chat panel should not require API credits

The first version of the panel only spoke to the Anthropic Messages API, which bills as API
credits - separate from a Claude subscription. That is the wrong default for someone who already
pays for Claude.

`IChatBackend` now abstracts "answer this message using the HexManiac tools", with two
implementations, and the panel prefers the one that costs nothing extra:

| | `ClaudeCliBackend` (preferred) | `ApiChatBackend` (fallback) |
| --- | --- | --- |
| Runs | the user's installed Claude Code, headless | the Messages API directly |
| Billing | their Claude subscription | API credits |
| Needs | `npm install -g @anthropic-ai/claude-code` | an API key |
| Tools reach the ROM | over loopback, through our own MCP server | in-process, same registry |

`ClaudeCliBackend` spawns `claude -p "<message>" --output-format stream-json --verbose
--mcp-config <written temp file> --allowedTools mcp__hexmaniac`, and carries `--resume <session-id>`
on later turns so the conversation continues. Because Claude Code reaches the tools over HTTP, the
panel starts the MCP server on demand (`EnsureMcpServerUrl`) rather than at launch, and writes the
config file itself since the port can move.

`--allowedTools mcp__hexmaniac` is deliberately narrow: headless mode would otherwise stall on a
permission prompt nobody can see, and pre-approving only this editor's own tools is a much smaller
grant than `--dangerously-skip-permissions`. Every one of those tools is undoable and none writes
to the .gba.

A side benefit: stream-json arrives message by message, so the CLI backend shows text and tool
lines as they happen. The "no streaming" limitation noted in Round 10 applies only to the API
fallback.

### Verified

With a stand-in `claude` on PATH (nothing installed on the machine), driving the real editor:
the panel detected it, started the MCP server (`MCP on port 8261` appeared in the title bar),
wrote `chat-mcp-config.json` pointing at that port, and spawned

    claude -p 'What ROM is open?' --output-format stream-json --verbose --mcp-config <path> --allowedTools mcp__hexmaniac

The transcript rendered assistant text, a dimmed tool line (`rom_status`, with the
`mcp__hexmaniac__` prefix stripped) and the final answer; the status line read "Claude Code
(claude) - uses your Claude subscription, not API credits" and the model dropdown greyed out,
since Claude Code picks its own. A second message carried `--resume sess-abc123`.

With neither backend present, the panel explains both options, recommending Claude Code first.

**Not verified:** a real Claude Code round trip, which needs the CLI actually installed - that is
the user's call to make, not something the editor or this port should do for them.

## Round 10c - the model dropdown was dead on the Claude Code backend

User report: "only lets me use sonnet 5 instead of opus 5 and i cant change it."

My fault, and a wrong assumption rather than a bug: when the Claude Code backend was added I greyed
the dropdown out with the placeholder "Claude Code picks the model". It does not have to - headless
mode takes `--model`, and the CLI wants a short alias (`opus`, `sonnet`, `haiku`) where the Messages
API wants a full id (`claude-opus-5`).

* `ChatModel` now carries both names, so one dropdown drives either backend.
* `ClaudeCliBackend.Model` appends `--model <alias>`; null leaves Claude Code on its own default.
* The dropdown is enabled for both backends and `ApplyModel` routes the choice to whichever is
  answering. Opus is listed first; Sonnet stays the default pick.
* The choice is now remembered across sessions in `chat-model.txt`. A panel that forgets which
  model you wanted every time you open it is a panel you fight with.
* The status line spells out what is answering: "Claude Code / opus - uses your Claude
  subscription, not API credits".

Verified against the user's real Claude Code 2.1.273: with the remembered model set to opus, the
panel started on "Opus 5 - most capable" and the assistant replied "I'm Claude Opus 5 (model ID
`claude-opus-5`), running in Claude Code."

Worth recording for later: `FindExecutable` checks `~/.claude/local/claude`, `~/.local/bin/claude`,
the Homebrew paths and `/usr/local/bin` *before* PATH. That ordering meant a stand-in `claude` on
PATH was correctly ignored once the real one existed - convenient here, but the reason a PATH-based
test stops working the moment Claude Code is really installed.

## Round 11 - polish, found by diffing bindings rather than by eye

Rather than hunting for gaps visually, this round extracted every `{Binding}` path, `Path=` and
`MethodCommand` from each WPF `.xaml` and diffed it against the ported `.axaml`. 96 raw
differences, most of them WPF-isms that have a different spelling here rather than missing work
(`ActualWidth` -> `Bounds`, `ElementName` -> `#name`, `IsMouseOver` -> `:pointerover`,
`Visibility` -> `IsVisible`, `RenderTransform`). What survives that filter is a real to-do list,
and it is worth keeping.

### Fixed this round: the code editor's right-click menu

Right-clicking in the code tool's script editor should offer **Goto Address**, **Goto Source** and
**Find Uses**, depending on what the caret is on. The port had none of them - and interestingly,
`TextEditor.ContextMenuOverride` was already ported *and* already bound to the inner TextBox. Only
the consumer was missing, so nothing ever set it.

WPF used three `DataTrigger`s that each swapped the entire ContextMenu, which gave precedence for
free: its own comment notes "GotoSource must override GotoAddress in order for Goto trainerstats/ED
to work right for Emerald". Avalonia shows or hides items independently, so that precedence has to
be stated - hence the new `AndNotConverter`, and Goto Address binding to
`CanGotoAddress && !CanGotoSource`.

Bindings go through `#TextEditor.Tag.*` rather than the DataContext, because the editor's own
DataContext is the text `Editor` while these commands live on the `CodeBody` that `Tag` points at.

**Verified** by driving the running editor through its own MCP server: opened the code tool in
Script mode on a real event script (`0x1604BC`), which realized the template - **no binding errors**,
so every new path resolves. Then walked the caret down the script and watched the flags the menu
binds to:

```
caret on 'section0: # 1604BC'   gotoAddr=True   gotoSrc=False  findUses=False
caret on '  lock'               gotoAddr=False  gotoSrc=False  findUses=False
caret on '  setvar varResult 1' gotoAddr=False  gotoSrc=False  findUses=False
```

So on an address token exactly one item appears, and on ordinary command lines the menu is empty -
which is what WPF does.

**Not verified:** the physical right-click. Background automation refuses to open context menus, so
the menu appearing needs a human with a mouse.

### The remaining list, in rough order of how much a user would notice

* **TabView: no horizontal scrollbar.** `ShowHorizontalScroll`, `HorizontalScrollValue`,
  `HorizontalScrollMaximum`, `DesiredHorizontalViewportSize` are all unbound in the port, so wide
  tables cannot be scrolled sideways.
* **PaletteControl: no context menu** - WPF has Copy / Paste / Delete Color.
* **ImageEditorView: no context menu** - Copy / Paste / Select All / Delete.
* **MainWindow odds and ends**: `IsNewVersionAvailable` + `NewVersionAcknowledged` (the Update
  menu item and its "seen" marker), `RecentFileMenuEnabled`, `SmallMode`, `ToggleMatrix`,
  `HideSearchControls`, `IsMetadataOnlyChange`, `ShortName`.
* **MapTab**: `GotoMapNames`, `PointerAddressText`, `ScriptAddressError`, `Palettes` /
  `PaletteSelection`, `PanCommand`.
* `ToolPanelWidth` is not persisted, so the tool panel's width resets between sessions.

Two of the diff's hits were false alarms worth recording so they are not chased again: MapTab's
`DrawMultipleTiles` and `BlockEditorVisible` *are* bound, but inside `<Binding Path="..."/>`
elements of a MultiBinding, which the extraction regex does not match; and the WPF-only
`Annotate` / `AddressShowMenu` / `HandleComboClick` handlers are dead code upstream - nothing in
HexManiac.WPF's own XAML references them.

## Round 12 - correcting Round 11's list, then clearing what was really left

### The horizontal scrollbar was never missing

Round 11 named it the biggest gap. It was wrong, twice over, and both errors were in the
measurement rather than the code:

1. **"It is missing."** The binding-extraction regex only matched `{Binding Path...}` where the
   path starts with a letter. The port writes `{Binding #HexContent.HorizontalScrollValue}` -
   Avalonia's element-name form, starting with `#` - so every one of those four bindings was
   invisible to the diff. The ScrollBar was fully ported all along.
2. **"It is there but broken."** Driving it live, setting the bar's `Value` left
   `HexContent.HorizontalScrollValue` at 0. That looked like a dead TwoWay binding. It was the
   200ms transition on that property (the port's stand-in for WPF's DoubleAnimation): the read
   happened while the value was still animating from 0. Re-reading after the transition settles
   gives exactly 400.0.

Verified working: with row width forced to 64 on a 580px-wide control, `ShowHorizontalScroll=True`,
`HorizontalScrollMaximum=1148.5`, and the bound ScrollBar reports `IsVisible=True Max=1148.5
Viewport=579.5`. Setting the bar moves the content.

**Method fix.** Searching for `{Binding X}` patterns produces false positives in both directions.
The reliable check is to grep the ported control - markup *and* code-behind - for the member name
in any spelling. Re-running the whole Round 11 list that way collapsed it from twelve suspects to
seven, and all of these turned out to be already ported: PaletteControl's Copy/Paste/DeleteColor,
ImageEditorView's Copy/Paste/SelectAll, `ToolPanelWidth`, `SearchBytes`, `ShowMatrix`,
`ToggleMatrix`, `CalculateHashes`, MapTab's `ScriptAddressError` and `PanCommand`.

### Actually missing, and now fixed

* **Open Recent never greyed out.** `RecentFileMenuEnabled` was unbound, so the menu looked
  available with nothing behind it.
* **Help > Update was always enabled**, whether or not there was a new version.
  `IsNewVersionAvailable` now drives it. *Documented as different:* WPF also tinted the item with
  the accent colour until the user moused over it (`NewVersionAcknowledged`); a NativeMenuItem is
  drawn by macOS and has no Background, so only the enable/disable half carries over - which is the
  half that means something.
* **Escape did not close the find bar.** WPF hung that KeyBinding off the search-results
  TabControl's `InputBindings`; the port has no such control, so it was lost. It is now a
  window-level binding, which is what a user expects from Escape anyway. The goto box keeps its own
  Escape binding and still wins while it has focus, being nearer the focused element.

Verified live: no binding errors; `RecentFileMenuEnabled=True`, `IsNewVersionAvailable=True`,
`HideSearchControls.CanExecute=True`; and `ShowFind` -> find bar visible -> `HideSearchControls` ->
hidden.

### Still genuinely missing

* MapTab `GotoMapNames` - the button beside Map Name that jumps to the map-names table.
* MapTab `PointerAddressText` - a pointer address readout in the events panel.
* MapTab `Palettes` / `PaletteSelection` - the palette picker combo in the block editor.
* `SmallMode` - a layout variant WPF switches on with a DataTrigger.

## Round 13 - the last four, and a feature I had wrongly deleted

### Fixed

* **`SmallMode`** - the goto panel's shortcut buttons now shrink from 100x100 to 50x50 when the
  view model asks. WPF used a DataTrigger; the port carries it as a style class on the button
  (`Classes.small`) with the setters in `Window.Styles`. `SmallMode` lives on each
  `GotoShortcutViewModel`, not on the panel, so the binding sits in the item template as WPF's did.
* **`GotoMapNames`** - the small right-arrow beside the Map Name combo, which jumps to the
  map-names table so you can edit a name rather than only pick one. Verified by running the command:
  the editor switches to the ROM tab at that table.
* **`Palettes` / `PaletteSelection`** - the block editor's palette picker and its
  "Selected Palette: (n)" readout. Verified live against Pallet Town: `Palettes=13`,
  `PaletteSelection=0`, the ComboBox in the visual tree reports 13 items with `SelectedIndex=0`, and
  the readout renders `Selected Palette:(0)`.

### A feature an earlier round deleted by mistake

`PointerAddressText` turned out to be the visible half of a block this port had **removed**. An
earlier round listed `HasBasicFlag`, `BasicFlagText`, `HasBasicItem`, `BasicItem` and `BasicText`
as "invented Core property names (dead bindings)" and cut them. Checking each against Core this
round: **all five exist** on `IEventViewModel`. Only `HasBasicText` does not - and WPF never used
it. So the cut removed a working feature on the strength of one wrong name.

Restored: for a script too elaborate to edit fully in the map editor, the events panel again offers
the flag, the item and each text the script uses (with its pointer address above it), instead of
just the "use the script editor" notice.

The lesson is the same one Round 12 taught about the scrollbar: *check the member against Core
before concluding a binding is dead.* Both mistakes came from trusting a pattern match over a
lookup.

### Verified

Opening Pallet Town realizes the whole MapTab template - the goto arrow, the palette picker and the
restored basic-event block all present, with **no binding errors** in the log.

### Remaining

Nothing left on the list that started in Round 11. The next pass should re-derive a fresh one with
the Round 12 method (grep the ported control for each member name, markup and code-behind, rather
than pattern-matching bindings).

## Round 14 - a real audit, and what it does and does not prove

Asked "is everything ported?", the honest answer needed a measurement rather than an impression -
this session has produced two confident wrong answers in each direction already.

**Method.** For every WPF view with a ported counterpart, extract every view-model member the
markup references (`{Binding X}`, `Path="X"`, `{res:MethodCommand X}`), drop the WPF framework
names, and check each remaining name appears *somewhere* in the ported control - markup or
code-behind. This is the Round 12 method, applied to everything.

**Result: 27 views, 26 fully covered.** The single hit on the 27th (`MainWindow: ShortName`) is a
false positive: it is used, in the shared `MenuConverters.cs`, which a per-control search does not
look at.

### Fixed on the way to that number

* **The tile picker had no selection highlight at all.** `TileSelectionX/Y/Toggle` were unbound, so
  clicking a tile in the block editor gave no feedback. Added the rectangle, positioned by
  TranslateTransform, with the same 3x-to-1x pop the block highlight uses. The toggle lives on
  `BlockEditor`, which is replaced whenever the primary map changes, so the subscription follows it.
* **Ctrl+Z / Ctrl+Y in the table tool's stream boxes** went to the TextBox's own undo instead of the
  ROM's change history. (`UndoLimit=0` was already ported - the KeyBindings that make that
  meaningful were not.)
* **Script Address error text** was never shown. The box turned red via `HasScriptAddressError` but
  `ScriptAddressError` - the message saying *why* - was not in the tooltip. Fixed on all three
  script-address boxes.
* **The Save button never showed metadata-only changes.** WPF dropped the icon from Accent to
  Primary and added "(Only metadata changes)" to the tooltip; both restored.

### What this number does not prove

The audit measures *binding coverage*, which is a good proxy and a poor guarantee. It says nothing
about:

* **Code-behind behaviour** - event handlers and interaction logic. A name-diff of those earlier
  was noisy (most hits were WPF-only spellings or upstream dead code).
* **Whether a ported binding works at runtime** - only that the name is referenced.
* **Visual fidelity** - layout, spacing and colour are not in scope of any diff run so far.
* **Deliberate differences**, which remain as recorded: no splash screen, no hex scroll animation,
  pipe-based single-instance, some rich tooltips flattened, the eyedropper cannot sample other
  applications' windows, and the emulator and MCP/chat features have no WPF counterpart at all.

The honest summary: every view-model member HexManiac.WPF's markup uses is now referenced by the
port, and the features exercised this session work. That is not the same as "bug-free", and the
areas above are where an unported detail would still be hiding.

## Round 15 - the tools, audited by interaction rather than by binding

"I use all the tools" deserves a check aimed at the thing the Round 14 binding audit explicitly
could not see: **interaction logic**. Extracted every handler HexManiac.WPF wires from markup
(`Click=`, `MouseDown=`, `SelectionChanged=`, `KeyDown=`, ...) - **108 handlers across 16 views** -
and checked each against the ported control.

18 did not match by name. Triaging every one:

| Not matched | Verdict |
| --- | --- |
| ImageEditorView's 7 `Mouse*` handlers | ported, renamed to Avalonia pointer handlers (`ImagePointerPressed`, etc.) |
| SelectedBlockEditor's `MouseClickTile` / `MouseGrabTile` / `MouseExitTiles` | ported, renamed (`TilePointerPressed`, `MouseEnterTile`) |
| PaletteControl's `ShowElementPopup` / `HideElementPopup` | ported, rebuilt around an Avalonia `Popup` |
| TabView's `LeftClick` / `LeftDoubleClick`, MainWindow's `MiddleClick` | not handler names at all - `MouseAction` values on MouseBindings, which my regex over-matched |
| `AcknowledgeAccentItem` | cannot port: a NativeMenuItem is drawn by macOS and has no MouseMove |
| `ExitClicked` | not ported - macOS puts Quit in the application menu, where a Mac user looks for it |
| ThemeSelector's `ClearKeyboardFocus` | not ported; clicking the dialog background does not drop focus from its text box |

### Fixed: the middle-click gestures, which were missing entirely

WPF declared them as `MouseBinding`s, which Avalonia has no equivalent for - so all three were
lost, and none showed up in the binding audit because a MouseBinding references a command, not a
member the port was missing elsewhere:

* **Middle-click a tab to close it.** Read off the pointer in `TabMouseDown`.
* **Middle-click the message bar to dismiss it.**
* **Middle-click the error bar to dismiss it.**

### All five tools verified working

Against FireRed at `data.pokemon.stats`, each tool selected in turn, checking both the view model
and that its panel actually realizes:

```
Table  TableTool.Children = 90 groups        panel realized
Text   StringTool.Address = 0x254784         panel realized
Image  SpriteTool present (0px here - the stats table is not a sprite)
Code   CodeTool.Mode = Thumb                 panel realized
Logs   panel realized
```

No binding errors across any of the five.

### Honest status

Two independent audits now agree: every view-model member WPF's markup references is referenced by
the port (26/27 views, the last a false positive), and every markup-wired handler is either ported,
deliberately not ported for a platform reason, or was never a handler. All five tools populate and
render.

Still outside what either audit can see: handlers wired in WPF *code-behind* rather than markup,
and visual fidelity - spacing, colour and layout have never been diffed against WPF, only fixed
where something looked obviously wrong.

## Round 16 - the Utilities menu was dead

User report: everything under Utilities except Scripts is greyed out.

**Cause.** The three quick-edit submenus (Pokedex, Expand, Misc) were bound through
`QuickEditMenuConverter`, which built each `NativeMenuItem` with
`Command = item as ICommand ?? item.Command`. Neither exists: `IQuickEditItem` is not an ICommand
and has no Command property - it exposes `CanRun(viewPort)` / `Run(viewPort)` / `CanRunChanged`.
So every generated item got a null Command, and macOS greys out a menu item that has none. Scripts
was the only entry that worked because it binds a real command (`LaunchScriptsLocation`).

A converter could never have fixed this: building the command needs the editor and the window, and
a converter has neither. HexManiac.WPF does it in code (`FillQuickEditMenu` +
`CreateQuickEditCommand`), and so does the port now. A `NativeMenuItem` cannot be `x:Name`d, so the
three parents are located by header in the `NativeMenu` tree.

**Also restored: the confirmation dialog.** Running a quick edit is not meant to fire straight from
the menu. WPF opens a small window with the edit's description, a "Click here to learn more" link
when it has a wiki page, and Run/Cancel. That window was never ported, so even a working menu item
would have applied a ROM-wide edit with no warning. It is now `Windows/QuickEditDialog.axaml` -
declared rather than built in code, because the markup says what it is much more clearly.

`CanExecute` mirrors WPF exactly, including unwrapping a `MapEditorViewModel` to its ViewPort so
the edits stay available while a map tab is selected, and `CanRunChanged` re-raises
`CanExecuteChanged`.

### Verified

```
Pokedex   4 items, 3 enabled   e.g. Update Dex Conversion Table, Reorder National Dex
Expand    4 items, 4 enabled   e.g. Expand Rom, Make Tutors Expandable
Misc      3 items, 2 enabled   e.g. Render Rom Overview, Decapitalize Names
```

The few still disabled are correct - `CanRun` returns false for edits that do not apply to this
ROM. Executing "Make Tutors Expandable" opened `QuickEditDialog(Make Tutors Expandable)` and, on
being closed without confirming, left the ROM with no unsaved changes.

### Round 16b - the real reason they were greyed out

The fix above was necessary but not sufficient: the user reported the three submenus were *still*
greyed out. The first verification had checked the managed `NativeMenuItem` objects - submenu
present, commands enabled - which is the wrong layer. macOS renders from an exported NSMenu, and
asking the accessibility layer told a different story: `Utilities > Expand` was **disabled**, not
"a submenu with four entries".

**Cause.** `FillQuickEditMenus` assigned `parent.Menu = new NativeMenu()` from code. Avalonia's
macOS exporter does not pick that up - a submenu attached after the menu has been exported never
reaches the NSMenu, so the item renders as a plain entry with no command, which macOS greys out.
The give-away was that `Help > Online Reference Content`, whose submenu is declared in markup,
worked perfectly.

**Fix.** Declare an empty `<NativeMenu/>` for each of the three in XAML, and have the code *add
into that existing collection* instead of replacing it. A collection change is observed and does
reach the exported menu.

Verified through the accessibility layer, which is what the user actually sees:

```
Utilities > Expand   -> submenu: Expand Rom, Make Tutors Expandable, Make Moves Expandable, Add Tileset Animation
Utilities > Pokedex  -> submenu: Update Dex Conversion Table, Reorder National Dex, Reorder Regional Dex, Level Up Move Sort
```

Misc uses the identical code path and was already confirmed to hold 3 items.

**Lesson worth keeping:** for anything that crosses into a native macOS control - the menu bar
especially - verifying the Avalonia object model proves nothing. Check what the platform actually
rendered.

### Why two audits missed it

The binding audit saw `QuickEditsPokedex` referenced in the port and called it covered - it cannot
tell that the *converter consuming it* produced a dud. The handler audit only looks at markup-wired
handlers, and this was a binding. Both were measuring the right things and neither could see a
converter returning menu items with no command behind them.

## Round 17 - new features: keep-place reload, live memory, and a crash fix

### 1. State-preserving reload (done)

`Reload ROM` used to reboot to the title screen, so testing an edit cost a walk back to wherever
you were. `EmulatorSession.ReloadRom` now takes `keepPlace`: it serializes the console, loads the
edited ROM, and restores. A "Keep place" checkbox controls it, and the status line reports which
of the two actually happened - it cannot be guaranteed, because a save state describes memory as
the old ROM laid it out.

Measured first, in `scratchpad/retroprobe/keepplace.c`, because the whole premise depended on it:

```
serialize_size = 528448 bytes, serialize -> ok
after editing the ROM, unserialize -> ACCEPTED
frame after restore MATCHES the pre-edit frame
```

So mGBA accepts a state across a ROM edit, and you stay exactly where you were.

### 2. Live memory (data layer done, viewer not)

`LibretroCore` now exposes `MemoryRegions`, `ReadMemory` and `WriteMemory`, surfaced through the
session, the MainWindow bridge and a new MCP tool `emulator_memory` - so the chat panel and any MCP
client can read the running game's RAM.

**The important finding is which memory each core publishes.** libretro's region ids are nominal;
what they map to is the core's choice:

| core | SYSTEM_RAM | means |
| --- | --- | --- |
| mGBA | 32 KB | IWRAM at 0x03000000 |
| VBA-M, VBA-Next, gpSP | 256 KB | **EWRAM at 0x02000000** |

EWRAM is where a Pokemon game keeps flags, variables and the party, so the headline use - watching a
flag flip as you play - needs one of the latter three. mGBA does not publish a memory map
(`SET_MEMORY_MAPS`, env 36) either, so there is no way to reach EWRAM through it. `MemoryRegions`
infers the GBA address from the region size rather than hard-coding one core's layout, and the
tool's description tells the model which core it is talking to.

Verified live against mGBA: the region list reports Save RAM / IWRAM / VRAM with correct addresses,
and reads return live bytes.

**Not built:** a visual live-RAM window. Today the data is reachable through the chat panel and MCP,
not through the editor's own UI.

### 3. A crash worth more than the features

While testing, the editor died outright. The crash reporter from Round 6b earned its keep:

```
ArgumentException: upper value -1 is less than lower value 0
  at MapEditorViewModel.ToBoundedMapTilePosition
  at MapEditorViewModel.Hover
  at MapTab.ButtonMove
```

`ToBoundedMapTilePosition` computes `width = map.PixelWidth / 16 - borders`, then calls
`LimitToRange(0, width - 1)`. Before the map's first render `PixelWidth` is 0, the range becomes
(0, -1), and Core throws - so **a mouse move that landed in that window killed the editor**.

The handler is a faithful port of WPF's (XButton1 is the "no button held" sentinel in both, so plain
hovering calls Hover in both), and Core must not be modified, so the fix is to not make the call
until the map has a size: `MapReadyForPointer`, applied to the hover and the three drag paths.

## Round 18 - VBA-M made to work, which unlocks real live memory

Round 17 left live memory half-useful: mGBA only publishes 32KB of IWRAM, and the cores that
publish the 256KB of EWRAM - where a Pokemon game keeps flags, variables and the party - would not
start. Chasing that down found **three separate bugs, two of them mine**.

### 1. The wrong path was handed to the core

`CurrentRom()` passed the *tab's display name* as the ROM path. mGBA ignores it; VBA-M supports
gb/gbc/gba and reads the extension to decide which system the image is for, so with a name like
"Pokemon - FireRed Version (USA, Europe)" it simply refused to load. Now the real file path goes
across, and `LoadRom` appends `.gba` if a caller ever supplies something extensionless.

### 2. The ROM buffer was freed while the core was still reading it

`LoadRom` allocated a copy, passed it to `retro_load_game`, and freed it in a `finally`. libretro
says a core must copy what it needs - mGBA does, VBA-M keeps the pointer and reads through it while
it runs. That is a use-after-free, and it presented as a silent native crash with no managed
exception and no crash.log. The buffer now lives until the next load or until the core is disposed.

### 3. A libretro call that VBA-M cannot survive

Even then it died instantly. The macOS crash report named the frame:

```
EXC_BAD_ACCESS (SIGSEGV) KERN_INVALID_ADDRESS at 0x0
  vbam.libretro   retro_set_controller_port_device
```

VBA-M dereferences null inside it. The call was never needed - the spec says every port already
defaults to `RETRO_DEVICE_JOYPAD` - so it is gone. mGBA tolerated it, which is why this only
surfaced when a second core was tried.

**Worth remembering:** the managed crash reporter cannot see any of this. A native segfault leaves
no crash.log and no log line - the app simply vanishes. `~/Library/Logs/DiagnosticReports/*.ips` is
where the answer is, and it named the faulting function immediately.

### Result

VBA-M runs FireRed, and live memory is now genuinely useful:

```
Save RAM  0x0E000000 - 0x0E01FFFF  (131,072 bytes)
EWRAM     0x02000000 - 0x0203FFFF  (262,144 bytes)
VRAM      0x06000000 - 0x0601DFFF  (122,880 bytes)
```

Proved live rather than assumed: sampling five points across EWRAM three seconds apart, the chunk
at 0x02000000 changed while the rest held steady - the emulator is running and the reads follow it.

### Core picker

Cores can now be chosen from a dropdown in the emulator toolbar instead of by hand-editing
`core-path.txt`. It applies **next launch**, deliberately: two libretro cores cannot share a
process - loading a second one into a process that already has one segfaults, which is how bug 3
was found in the first place.

The trade-off is worth stating plainly in the UI, and the tooltip does: mGBA is the most accurate
core; VBA-M, VBA-Next and gpSP are the ones that expose EWRAM for live inspection.

## Round 19 - the live memory viewer

`Windows/MemoryViewerWindow.axaml(.cs)`, opened from **Memory** in the emulator toolbar.

The hex dump is the obvious half: region picker (EWRAM selected by default, being where the
interesting values live), an address box that accepts an address from any exposed region and
switches to whichever owns it, ASCII column, and a 5Hz refresh with **bytes that changed since the
last read drawn in the accent colour**. That alone makes a value visible the moment it moves.

The half that earns the window is **Snapshot / Compare**: remember the whole region, do something in
game, and see exactly which addresses changed and from what to what. It is the technique a cheat
search uses, except the answers are addresses you can then go and look at in the editor - which is
how you find where a flag or a variable lives without a disassembly. Each Compare re-bases the
snapshot, so pressing it repeatedly narrows on whatever keeps moving rather than re-reporting the
same churn.

Verified against FireRed running under VBA-M: the dump highlighted live changes as the intro played,
and Snapshot -> let the game run 6s -> Compare reported `6,310 byte(s) changed` with the list
(`02000000 00 -> 01`, `02000004 F0 -> BC`, ...).

The viewer closes with the emulator, since it reads that session's memory.

### Also fixed

The emulator window title became the ROM's full path once `CurrentRom` started handing the core a
real file path (Round 18). The core still gets the path; the title shows just the file name.

### Still to do from the feature list

* **Play-from-here** - now unblocked (`WriteMemory` exists and EWRAM is reachable), but it needs
  FireRed-specific knowledge of where the player's map and coordinates live in SaveBlock1.
* **Git-friendly project export** - not started.

## Round 20 - project export: a hack that can live in git

`Project/ProjectExport.cs`, reachable from **File > Export Project as Text... / Import Project from
Text...**, and as the MCP tools `project_export` / `project_import`.

A .gba is one opaque 16MB blob: two people cannot work on a hack at once, a change cannot be
reviewed, and "what did I alter last week" has no answer. Export writes one tab-separated file per
table plus a manifest, so all of that becomes an ordinary diff.

Deliberately a *data* export, not a serialisation of the ROM. Pointers are written for context and
a readable diff but never imported, because rewriting one means relocating what it points at -
the editor's job, not a text file's. Import states what it skipped instead of quietly doing half
the work, and refuses outright when the manifest's game code does not match the open ROM.

Everything import applies lands as **one entry in the undo history**, and nothing is written to the
.gba.

### Verified

```
export            -> 193 tables, 26,056 rows, 96,037 values, 1.1MB
edit the text     -> hp 45 -> 77, attack -> 123, BULBASAUR -> GITSAUR
import            -> "Applied 3 value(s) across 2 table(s)"
read the ROM back -> name=GITSAUR  hp=77  attack=123
```

And the check that matters most - exporting and re-importing without editing anything applies
**0 values** and leaves the tab reporting no unsaved changes. The round trip is lossless.

### A bug the import validation caught

The first import reported hundreds of `data.battle.text: a row has 1 columns, expected 2`. The
export was corrupting its own output: the pointer branch formatted its value *without escaping*,
and a pointer-to-text returns the text - which routinely contains newlines. Each one split a row
and silently lost every column after it.

Escaping that branch like any other cell fixed it: **0 malformed rows across all 193 tables**,
measured rather than assumed. Worth noting that this only surfaced because import validates its
input and says what it rejected; an importer that skipped bad rows quietly would have hidden a
data-loss bug indefinitely.

## Round 21 - save states, a reset that survives, and input that arrives

### Named save states on disk

**Emulator toolbar: a state dropdown + Save / Load / X**, backed by
`<AppSupport>/HexManiacAdvance/Emulator/states/<rom>/<name>.state`.

This is the answer to "I changed something on route 3 and I do not want to walk there again". The
thing actually asked for - drop the running game at an arbitrary map - is *not* reliably
deliverable, and the reason is worth writing down rather than rediscovering: it needs the player's
`gSaveBlock1Ptr` (IWRAM) and a way to make the game re-read the map, and no libretro core here
exposes both IWRAM and EWRAM, none of them answers `SET_MEMORY_MAPS`, and forcing a map reload
means executing game code. Save states have none of those problems: play to the spot once, name
it, and jump back to it after any edit, as many times as you like.

The list is ordered most-recently-written first and the newest entry is pre-selected, because a
dropdown that starts empty makes Load look broken.

Verified end to end: `title screen.state` (723,452 bytes) written; Reset cold-rebooted the game;
Load put the title screen straight back.

### Reset builds a new core

`EmulatorSession.Reset` throws the core away and starts a fresh one on the bytes the editor
currently holds. The lighter routes are not used: `retro_reset` is free to be a no-op and a reset
that silently declines is worse than no reset button, and `retro_unload_game` + `retro_load_game`
on the same instance is the same gamble. A new instance also picks up edits made since the game
started.

Verified on **mGBA**: Reset reboots to the title screen. **It does not work on VBA-M** - that core
keeps enough global state across `retro_deinit` / `retro_init` that even a brand new instance
carries on exactly where it was. There is no host-side fix for that, so the core picker's tooltip
says so and points at a save state instead. It is one more reason to prefer mGBA unless EWRAM
inspection is needed.

(The `retro_reset` crash that an earlier round blamed on `Gb_Apu::load_state` was the null log
callback. It no longer crashes.)

### Input: the core was never asking

**Symptom:** the emulator "did not recognise the keyboard". Nothing on the way in was wrong - the
window got the key, the map turned it into a button, the core called `retro_input_poll` and our
latch recorded it. The game still sat on the title screen.

The measurement that ended it was counting what the core *asks for*, not what we hand it. Over
thirty seconds VBA-M asked `retro_input_state` about joypad ids **12 and 13 and nothing else** -
L2 and R2, which a GBA does not have. It never asked about A, B, Start or a direction, so every
button read as released no matter what the keyboard did.

The cause is a chain of three, each one hiding the next:

1. The host declined `RETRO_ENVIRONMENT_GET_LOG_INTERFACE`. VBA-M stores whatever that returns and
   calls it unconditionally - it installs no fallback - so its `log_cb` stayed null.
2. `retro_set_controller_port_device` logs, so calling it jumped through that null pointer:
   SIGSEGV at address 0, no managed frame, editor gone. An earlier round read that as "VBA-M
   dereferences null inside it", removed the call, and wrote the reasoning into a comment - the
   spec does say a port already defaults to `RETRO_DEVICE_JOYPAD`.
3. Except VBA-M does not start polling the pad until it is told. Without the call it polls only
   its sensor ids forever.

Answering the log interface fixes all three: the callback is a `retro_log_printf_t` declared with
just its two named parameters (the varargs are never read, which is safe under cdecl and on arm64),
`retro_set_controller_port_device(0, JOYPAD)` is called once after `retro_load_game`, and the core
then asks about ids 0-15 like any other.

Verified one tap at a time: Enter left the title screen and Z carried on into the new-game intro as
far as the in-game CONTROLS screen - somewhere only real input reaches.

Two supporting changes kept from the same hunt:

- **A press is held for four polls** (`MinimumHoldPolls`). One frame is what the hardware needs and
  not what a person does; the shortest deliberate tap is 50-100ms, three to six frames.
- **`HandleInputState` answers `RETRO_DEVICE_ID_JOYPAD_MASK`**, the whole pad in one reply. VBA-M
  never asks for it, but mGBA and most modern cores do, and a core that asks and gets nothing reads
  every button as released - the exact bug above, wearing a different hat. The environment flag is
  deliberately *not* advertised: answering a question without claiming the feature works for cores
  on either side of it.

### Three wrong verdicts on the way, and what caused each

Worth recording, because each one looked conclusive:

1. **"Single taps work now."** They did not. Pressing A at the FireRed title screen must open the
   main menu; it never did. What looked like the game responding was the title screen timing out
   into its attract loop - Game Freak logo, copyright, title - on its own schedule, which happens
   to be a few seconds. **Lesson:** a screen that changes is not a screen that responded. Pick a
   state transition the input is the only cause of.
2. **"The window is behind another one."** True, and it was genuinely costing key events, but it
   was not the bug. Fixing a real problem that is not the reported one produces a confident report
   and an unchanged symptom.
3. **"Advertising bitmask support crashes VBA-M in `retro_init`."** Same null `log_cb`, reached by
   a different path. Two unexplained SIGSEGVs at address 0 from two unrelated entry points was the
   clue that should have been read as one cause, not two broken functions.

### A live input readout

The bottom bar ends with `key <last key>  -  <buttons>`: the last key the emulator window received,
and the buttons the console has. The button half reads what is held right now and falls back to the
last press the core sampled, because a press is four frames and the bar refreshes twice a second.

It exists because "the emulator does not read my inputs" is one symptom with several unrelated
causes, and nothing on screen told them apart. It is what turned this from guessing into
measuring - and its limit is worth remembering too: it showed a healthy `key Z - a` throughout,
because every link it covered really was working. The broken link was the one past it, on the
core's side, and finding that needed counting what the core asked for.

### "Click here to play", on the picture

A window that is not in front receives no key events at all, and that was genuinely happening here
as well - the editor, or this very chat, sitting over the game. The status bar says so, but the
status bar is the first strip another window covers, so the message is also drawn **over the
picture**, dimmed and centred, whenever the window is inactive. It is not hit-testable, so the
click that dismisses it is the click that focuses the game. `Deactivated` also releases held
buttons, since a key held as the window goes to the back never gets its key-up.

### Polish pass

- **The toolbar no longer clips its own status.** It printed `59.8 fps - mGBA 0.10.x`, which ran
  past the right edge and took the frame rate and any note with it. The core's name is in the
  picker immediately to its left, so the status line drops it.
- **Reset, Reload ROM and Load state resume a paused emulator.** All three change where the game
  is, and all three were invisible while paused - the picture is whatever frame was drawn last, so
  the button looked dead.
- **Reset says "restarted the core"**, because on a core that ignores it the only other feedback
  is a picture that did not change.
- The emulator window opens at 960x620 rather than 740x560, which is what it takes for its own
  toolbar to fit.

### A note on verifying this by automation

Two of the wrong verdicts in this round came from the test harness, not the code. A synthetic
`click at` from System Events reports the AX element under the point, but Avalonia only acts on the
*second* one - the first merely establishes pointer-over, which a real mouse gets for free on the
way. Several "Reset does nothing" readings were clicks that never reached the button at all. When
driving this app from a script, click twice, and confirm the handler ran (the status note) before
concluding anything about what it did.

### Correction: the command line is fine

An earlier draft of this section claimed a .gba passed on the command line does not open, on the
strength of a `kLSApplicationNotFoundErr` in the log and a window that looked empty. Both readings
were wrong: the LaunchServices line is macOS separately declining to route the file as a document
and has no bearing on argv, and the "empty" window was the Goto panel that HMA shows over a
freshly opened tab. Instrumented at the point that matters, the path arrives, the file loads, and
the tab opens - `loadedFile=16777216, tabs=1, selected=0`.

## Round 22 - the first real .app

`./publish-mac.sh` builds a self-contained, ad-hoc-signed `HexManiacAdvance.app` (127MB, arm64,
`LSMinimumSystemVersion` 11.0, `.gba` declared as a document type). Until this round it had never
been run, and neither had a Release build of any of this code. Both bugs below were invisible in
every Debug run and would have been the first two things a stranger hit.

### The bundle shipped without Core's resources

HexManiac.Core's `Singletons` constructor reads `resources/hma.py` next to the executable. The
project copies that folder in a target hooked to `AfterTargets="Build"` - but `dotnet publish`
assembles PublishDir from the publish item lists rather than by copying OutDir, so the published
app had no `resources` folder at all. The constructor runs before any window exists, so the failure
was an unhandled `DirectoryNotFoundException` and a bundle that died on launch with nothing on
screen. Fixed with a matching `CopyHmaResourcesToPublish` target; 35 files and the Scripts tree now
land in the bundle.

### Opening a .gba with the app did nothing

The Info.plist advertises `.gba`, so Finder offers HexManiacAdvance for a ROM - and it opened an
empty editor. macOS does not put those files in argv; it sends the running process an
open-documents event, which Avalonia surfaces as `FileActivatedEventArgs`. There was a TODO about
this that nobody could hit before, because a bare executable is never asked to open a document.

The subtlety: `ApplicationLifetime as IActivatableLifetime` compiles and always yields null - the
desktop lifetime does not implement it, and Avalonia's own summary on the interface says to use
`Application.Current.TryGetFeature<IActivatableLifetime>()` instead. The first fix built cleanly
and changed nothing, which is the worst kind.

### Verified on the built .app, from a clean directory, with no dev environment

```
open -a HexManiacAdvance.app "Pokemon - FireRed Version (USA, Europe).gba"
  -> ROM opens, Goto panel populated from the model
  -> Test in Emulator -> 59.8 fps
  -> Enter, then Z -> past the title screen and through the in-game CONTROLS pages
```

### Still outstanding for a release

- The app is **ad-hoc signed, not notarized**. Downloaded from GitHub it will be quarantined, and
  right-click > Open no longer clears that on current macOS. The README needs
  `xattr -dr com.apple.quarantine HexManiacAdvance.app`.
- The README says nothing about macOS: it should cover the quarantine line, that the emulator needs
  RetroArch installed for its cores, and that Reset does nothing on VBA-M.
- `dotnet test` still cannot build on macOS (the test project targets net6.0 and resolves its Core
  reference through Windows path separators). No test has ever run against this port.
