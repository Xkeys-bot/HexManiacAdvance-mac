using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HavenSoft.HexManiac.Core;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <example>
/// {hs:RotateTransform 60, 0, 0}
/// </example>
/// <remarks>
/// WPF's RotateTransform carries its own center point. Avalonia's takes only an angle and reads
/// the center from the target's RenderTransformOrigin, so the center is applied here by wrapping
/// the rotation in translations, which produces the same matrix.
/// </remarks>
public class RotateTransformExtension {
   public double Angle { get; }
   public double CenterX { get; }
   public double CenterY { get; }

   public RotateTransformExtension(double angle, double centerX, double centerY) {
      Angle = angle;
      CenterX = centerX;
      CenterY = centerY;
   }

   public RotateTransformExtension(double angle) : this(angle, .5, .5) { }

   public object ProvideValue(IServiceProvider serviceProvider) {
      if (CenterX == 0 && CenterY == 0) return new RotateTransform(Angle);
      var matrix = Matrix.CreateTranslation(-CenterX, -CenterY)
         * Matrix.CreateRotation(Matrix.ToRadians(Angle))
         * Matrix.CreateTranslation(CenterX, CenterY);
      return new MatrixTransform(matrix);
   }
}

/// <example>
/// {hs:Geometry 'M0,0 L0,1 1,1 1,0 Z'}
/// </example>
public class GeometryExtension {
   public string Figures { get; }

   public GeometryExtension(string figures) { Figures = figures; }

   public object ProvideValue(IServiceProvider serviceProvider) => Geometry.Parse(Figures);
}

public class TextGeometryExtension {
   public string Text { get; set; }
   public Point Location { get; set; }
   public int Size { get; set; }

   public TextGeometryExtension() { }
   public TextGeometryExtension(string text, int x, int y, int size) => (Text, Location, Size) = (text, new Point(x, y), size);

   public object ProvideValue(IServiceProvider serviceProvider) {
      var formattedText = new FormattedText(
         Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
         new Typeface("Calibri"), Size, Brushes.Black);
      return formattedText.BuildGeometry(Location);
   }
}

/// <summary>
/// Icons are taken from Icons.axaml, which is merged into the application resources.
/// WPF stored them as x:Name'd children of an ItemsControl and looked them up with FindName;
/// Avalonia has no FindName on a resource, so they are keyed resources instead.
/// </summary>
/// <example>
/// {hs:Icon Save}
/// </example>
public class IconExtension {
   public const string ResourcePrefix = "Icon.";

   public string Name { get; }

   public IconExtension(string name) { Name = name; }

   public object ProvideValue(IServiceProvider serviceProvider) => GetIcon(Name);

   /// <summary>
   /// Returns a private copy of the named icon, never the resource itself.
   ///
   /// Avalonia's Shape applies its Stretch by assigning Transform to the Geometry it is handed,
   /// which mutates the shared resource. The next Path that uses the same icon then tries to
   /// Clone() an already-transformed geometry and throws
   /// "Unable to cast TransformedGeometryImpl to IStreamGeometryImpl" - which took the whole app
   /// down the moment two map connection shifters wanted the same arrow.
   /// </summary>
   public static Geometry GetIcon(string name) {
      if (Application.Current == null) return null;
      if (!Application.Current.TryGetResource(ResourcePrefix + name, Application.Current.ActualThemeVariant, out var resource)) return null;
      if (resource is not Geometry geometry) return null;
      try {
         return geometry.Clone();
      } catch (NotSupportedException) {
         // A geometry type that cannot be cloned is still safe to share as long as nothing
         // stretches it; returning it unchanged keeps the old behaviour for those.
         return geometry;
      }
   }
}

/// <summary>
/// Creates a Command from a method on the ViewModel.
/// </summary>
public class MethodCommandExtension {
   public string CommandMethod { get; }

   public MethodCommandExtension(string methodName) => CommandMethod = methodName;

   public object ProvideValue(IServiceProvider serviceProvider) {
      if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget valueProvider) return null;
      if (valueProvider.TargetObject is not StyledElement element) {
         // Inside a template WPF returned the extension itself to defer evaluation. Avalonia
         // assigns the result straight into an ICommand slot (NativeMenuItem.Command, say), so
         // returning the extension throws InvalidCastException -- return null instead.
         return null;
      }
      var command = new MethodCommand(element.DataContext, CommandMethod);
      element.DataContextChanged += (sender, e) => command.Context = element.DataContext;
      return command;
   }
}
