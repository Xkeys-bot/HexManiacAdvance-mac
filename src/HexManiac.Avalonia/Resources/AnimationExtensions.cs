using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;

namespace HavenSoft.HexManiac.AvaloniaUI.Resources;

/// <summary>
/// Port of the AnimationExtensions in HexManiac.WPF/Controls/TutorialControl.xaml.cs.
///
/// WPF called element.BeginAnimation(SomeProperty, new DoubleAnimation(...)) imperatively, with
/// AccelerationRatio / DecelerationRatio for easing. Avalonia has no per-element BeginAnimation
/// for a one-off value change; the equivalent is to give the element a Transition on that
/// property and then just assign the value. WPF's acceleration/deceleration ratios map onto
/// Avalonia easing curves, so the motion is comparable rather than numerically identical.
/// </summary>
public static class AnimationExtensions {
   private static readonly TimeSpan Time = TimeSpan.FromSeconds(.4);

   private static void EnsureTransition(Animatable element, AvaloniaProperty property, Easing easing) {
      element.Transitions ??= new Transitions();
      foreach (var existing in element.Transitions) {
         if (existing is DoubleTransition d && d.Property == property) { d.Easing = easing; return; }
      }
      element.Transitions.Add(new DoubleTransition { Property = property, Duration = Time, Easing = easing });
   }

   public static void AnimateTop(this Control element, double position) {
      // WPF split the easing between acceleration and deceleration based on the distance.
      var lag = Math.Min(1, position / 450);
      EnsureTransition(element, Canvas.TopProperty, lag > .5 ? new CubicEaseIn() : new CubicEaseOut());
      Canvas.SetTop(element, position);
   }

   public static void AnimateOpacity(this Control element, double from, double to, double acceleration) {
      EnsureTransition(element, Visual.OpacityProperty, acceleration > 0 ? new CubicEaseIn() : new LinearEasing());
      if (!double.IsNaN(from)) element.Opacity = from;
      element.Opacity = to;
   }

   public static void AnimateLeft(this Control element, double position) {
      EnsureTransition(element, Canvas.LeftProperty, new LinearEasing());
      Canvas.SetLeft(element, position);
   }

   public static void AnimateRight(this Control element, double from, double to) {
      EnsureTransition(element, Canvas.RightProperty, new CubicEaseOut());
      Canvas.SetRight(element, from);
      Canvas.SetRight(element, to);
   }
}
