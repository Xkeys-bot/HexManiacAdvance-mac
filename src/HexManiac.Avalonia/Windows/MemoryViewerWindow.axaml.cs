using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using HavenSoft.HexManiac.AvaloniaUI.Emulation;

namespace HavenSoft.HexManiac.AvaloniaUI.Windows;

/// <summary>
/// Watches the running game's memory.
///
/// The hex dump is the obvious half. The half that earns the window is Snapshot/Compare: remember a
/// region, do something in game, and see exactly which addresses changed. That is how you find
/// where a flag or a variable lives without a disassembly - the same technique a cheat search uses,
/// except the results are addresses you can then look at in the editor.
/// </summary>
public partial class MemoryViewerWindow : Window {
   private const int BytesPerRow = 16;
   private const int VisibleRows = 24;
   private const int VisibleBytes = BytesPerRow * VisibleRows;

   private Func<IReadOnlyList<MemoryRegion>> getRegions;
   private Func<uint, int, int, byte[]> read;

   private IReadOnlyList<MemoryRegion> regions = Array.Empty<MemoryRegion>();
   private MemoryRegion region;
   private int offset;

   private byte[] previous;             // for highlighting what moved between refreshes
   private byte[] snapshot;             // for Snapshot/Compare
   private MemoryRegion snapshotRegion;

   private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };

   public MemoryViewerWindow() {
      InitializeComponent();
      timer.Tick += (sender, e) => Refresh();
   }

   public void Attach(Func<IReadOnlyList<MemoryRegion>> getRegions, Func<uint, int, int, byte[]> read) {
      this.getRegions = getRegions;
      this.read = read;
      PopulateRegions();
      Refresh();
      timer.Start();
   }

   private void PopulateRegions() {
      regions = getRegions?.Invoke() ?? Array.Empty<MemoryRegion>();
      RegionBox.ItemsSource = regions.Select(r => $"{r.Name} ({r.Size / 1024}KB)").ToList();
      if (regions.Count == 0) return;
      // EWRAM first if it is there: it is where the interesting values live.
      var index = regions.ToList().FindIndex(r => r.Name == "EWRAM");
      RegionBox.SelectedIndex = index >= 0 ? index : 0;
   }

   private void RegionChanged(object sender, SelectionChangedEventArgs e) {
      if (RegionBox.SelectedIndex < 0 || RegionBox.SelectedIndex >= regions.Count) return;
      region = regions[RegionBox.SelectedIndex];
      offset = 0;
      previous = null;
      AddressBox.Text = $"0x{region.GbaAddress:X8}";
      Refresh();
   }

   private void AddressKeyDown(object sender, KeyEventArgs e) {
      if (e.Key is Key.Return or Key.Enter) { GoToAddress(this, null); e.Handled = true; }
   }

   private void GoToAddress(object sender, RoutedEventArgs e) {
      if (region == null) return;
      var text = (AddressBox.Text ?? string.Empty).Trim();
      if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
      if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)) return;

      // Accept an address in any exposed region and switch to whichever one owns it, so pasting an
      // address from the change list or from the editor just works.
      var owner = regions.FirstOrDefault(r => address >= r.GbaAddress && address < r.GbaAddress + r.Size);
      if (owner != null && owner != region) {
         region = owner;
         RegionBox.SelectedIndex = regions.ToList().IndexOf(owner);
      }
      if (region == null || address < region.GbaAddress || address >= region.GbaAddress + region.Size) return;

      offset = (int)(address - region.GbaAddress) / BytesPerRow * BytesPerRow;
      previous = null;
      Refresh();
   }

   private void Refresh() {
      if (region == null || read == null) return;
      if (LiveBox.IsChecked != true && previous != null) return;

      var bytes = read(region.Id, offset, VisibleBytes);
      if (bytes == null) return;

      var inlines = new InlineCollection();
      var changedBrush = this.FindResource("Accent") as IBrush ?? Brushes.Orange;
      var plainBrush = this.FindResource("Primary") as IBrush ?? Brushes.White;
      var addressBrush = this.FindResource("Secondary") as IBrush ?? Brushes.Gray;

      for (int row = 0; row * BytesPerRow < bytes.Length; row++) {
         var rowStart = row * BytesPerRow;
         inlines.Add(new Run($"{region.GbaAddress + offset + rowStart:X8}  ") { Foreground = addressBrush });
         var ascii = new StringBuilder();
         for (int i = 0; i < BytesPerRow && rowStart + i < bytes.Length; i++) {
            var value = bytes[rowStart + i];
            var moved = previous != null && rowStart + i < previous.Length && previous[rowStart + i] != value;
            inlines.Add(new Run($"{value:X2} ") { Foreground = moved ? changedBrush : plainBrush });
            ascii.Append(value >= 0x20 && value < 0x7F ? (char)value : '.');
         }
         inlines.Add(new Run(" " + ascii + Environment.NewLine) { Foreground = addressBrush });
      }

      DumpText.Inlines.Clear();
      DumpText.Inlines.AddRange(inlines);
      previous = bytes;
   }

   #region Snapshot / compare

   private void TakeSnapshot(object sender, RoutedEventArgs e) {
      if (region == null) return;
      snapshot = read(region.Id, 0, region.Size);
      snapshotRegion = region;
      SnapshotStatus.Text = snapshot == null
         ? "could not read that region"
         : $"snapshot of {region.Name} taken ({snapshot.Length:N0} bytes) - now do something in game, then Compare";
      ChangeList.Text = string.Empty;
   }

   private void CompareSnapshot(object sender, RoutedEventArgs e) {
      if (snapshot == null || snapshotRegion == null) {
         SnapshotStatus.Text = "take a snapshot first";
         return;
      }
      var now = read(snapshotRegion.Id, 0, snapshot.Length);
      if (now == null) { SnapshotStatus.Text = "could not read that region"; return; }

      var changes = new List<string>();
      var count = 0;
      for (int i = 0; i < snapshot.Length && i < now.Length; i++) {
         if (snapshot[i] == now[i]) continue;
         count++;
         if (changes.Count < 400) {
            changes.Add($"{snapshotRegion.GbaAddress + i:X8}  {snapshot[i]:X2} -> {now[i]:X2}");
         }
      }

      SnapshotStatus.Text = count == 0
         ? "nothing changed"
         : $"{count:N0} byte(s) changed" + (count > changes.Count ? $" (showing the first {changes.Count})" : string.Empty);
      ChangeList.Text = string.Join(Environment.NewLine, changes);
      // Compare again from here, so repeated presses narrow in on what keeps moving.
      snapshot = now;
   }

   private void ClearSnapshot(object sender, RoutedEventArgs e) {
      snapshot = null;
      snapshotRegion = null;
      ChangeList.Text = string.Empty;
      SnapshotStatus.Text = string.Empty;
   }

   #endregion

   protected override void OnClosed(EventArgs e) {
      timer.Stop();
      base.OnClosed(e);
   }
}
