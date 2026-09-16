using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HavenSoft.HexManiac.AvaloniaUI.Resources;
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Map;

namespace HavenSoft.HexManiac.AvaloniaUI.Controls;

/// <summary>
/// Ported from HexManiac.WPF/Controls/TutorialControl.xaml.cs.
///
/// WPF drove this entirely with DoubleAnimations and ItemContainerGenerator lookups. Avalonia has
/// neither imperative BeginAnimation nor ItemContainerGenerator, so card movement goes through
/// AnimationExtensions (property transitions) and containers are found with ContainerFromItem.
/// </summary>
public partial class TutorialControl : UserControl {
   private const double ElementHeight = 90;
   private const double SlideDistance = -300;

   public event EventHandler<MapTutorialViewModel> EnterTutorial;

   public MapTutorialsViewModel ViewModel => DataContext as MapTutorialsViewModel;
   private MapTutorialsViewModel subscribed;

   public TutorialControl() {
      InitializeComponent();
      DataContextChanged += (sender, e) => UpdateTutorialHandlers();
      PointerEntered += (sender, e) => Tutorials.AnimateLeft(SlideDistance);
      PointerExited += (sender, e) => Tutorials.AnimateLeft(0);
   }

   private void UpdateTutorialHandlers() {
      if (subscribed != null) {
         foreach (var tut in subscribed.Tutorials) {
            tut.PropertyChanged -= HandleTutorialChanged;
            tut.AnimateMovement -= HandleTutorialChanged;
         }
         subscribed.CompletedTutorial -= ShowCheck;
      }
      subscribed = ViewModel;
      if (subscribed != null) {
         foreach (var tut in subscribed.Tutorials) {
            tut.PropertyChanged += HandleTutorialChanged;
            tut.AnimateMovement += HandleTutorialChanged;
         }
         subscribed.CompletedTutorial += ShowCheck;
      }
   }

   private void HandleTutorialChanged(object sender, EventArgs e) {
      var tut = (MapTutorialViewModel)sender;
      var ui = Tutorials.ContainerFromItem(tut);
      if (ui == null) return;
      ui.IsVisible = true;
      ui.AnimateTop(tut.TargetPosition * ElementHeight);
      ui.AnimateOpacity(double.NaN, tut.Incomplete ? 1 : 0, 0);
      ui.AnimateLeft(tut.Incomplete ? 0 : 100);
   }

   private void ShowCheck(object sender, EventArgs e) {
      var tut = (MapTutorialViewModel)sender;
      if (tut.Index >= 5) return; // don't show the checkmark for off-screen tutorials
      var ui = Tutorials.ContainerFromItem(tut);
      if (ui == null) return;
      Canvas.SetTop(Check, tut.TopEdge + 15);
      var offset = 300 + Canvas.GetLeft(Tutorials);
      Check.AnimateRight(ui.Bounds.Width / 2 - offset, ui.Bounds.Width * 1.2 - offset);
      Check.AnimateOpacity(1, 0, 1);
   }

   /// WPF raised this from MouseEnter on each card's Border.
   private void OnEnterTutorial(object sender, PointerEventArgs e) {
      if (sender is not Control element) return;
      if (element.DataContext is not MapTutorialViewModel tutorial) return;
      EnterTutorial.Raise(this, tutorial);
   }
}
