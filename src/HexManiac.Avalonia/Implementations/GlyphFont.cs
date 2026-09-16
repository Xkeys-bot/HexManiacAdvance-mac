using Avalonia.Media;

namespace HavenSoft.HexManiac.AvaloniaUI.Implementations;

/// <summary>
/// WPF's GlyphTypeface exposed CharacterToGlyphMap, AdvanceWidths, Height and Baseline as
/// em-relative numbers. Avalonia's IGlyphTypeface exposes TryGetGlyph/GetGlyphAdvance in font
/// design units plus a FontMetrics. This shim restores the WPF shape so FormatDrawer's layout
/// arithmetic ports across unchanged.
/// </summary>
public class GlyphFont {
   public IGlyphTypeface Typeface { get; }

   /// Total line height as a fraction of the em size (WPF's GlyphTypeface.Height).
   public double Height { get; }

   /// Distance from the top of the line to the baseline, as a fraction of em (WPF's Baseline).
   public double Baseline { get; }

   readonly double designEmHeight;

   public GlyphFont(Typeface typeface) {
      Typeface = typeface.GlyphTypeface;
      var metrics = Typeface.Metrics;
      designEmHeight = metrics.DesignEmHeight;
      // Avalonia reports Ascent as negative (up from the baseline); WPF's ratios are positive.
      Height = (-metrics.Ascent + metrics.Descent + metrics.LineGap) / designEmHeight;
      Baseline = -metrics.Ascent / designEmHeight;
   }

   public bool TryGetGlyph(char character, out ushort glyph) => Typeface.TryGetGlyph(character, out glyph);

   /// Advance width in pixels at the given font size (WPF: AdvanceWidths[glyph] * size).
   public double AdvanceWidth(ushort glyph, double size) => Typeface.GetGlyphAdvance(glyph) * size / designEmHeight;
}
