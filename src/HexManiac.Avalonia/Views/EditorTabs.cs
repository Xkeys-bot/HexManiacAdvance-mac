using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Collections.Specialized;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.AvaloniaUI.Views;

/// <summary>
/// EditorViewModel is IEnumerable&lt;ITabContent&gt; + INotifyCollectionChanged, which is all WPF's
/// ItemsControl needs. Avalonia's ItemsSourceView additionally requires IList when a source
/// raises collection changes ("Collection implements INotifyCollectionChanged but not IList"),
/// so the TabControl binds to this mirror of the editor's tabs instead of to the editor itself.
/// </summary>
public class EditorTabs : ObservableCollection<ITabContent> {
   readonly EditorViewModel editor;

   /// <summary>Raised after the mirror has caught up with the editor's tab list.</summary>
   public event EventHandler Synced;

   public EditorTabs(EditorViewModel editor) {
      this.editor = editor;
      Resync();
      editor.CollectionChanged += OnEditorCollectionChanged;
   }

   void OnEditorCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
      switch (e.Action) {
         case NotifyCollectionChangedAction.Add:
            // EditorViewModel.Add always appends and reports no index.
            foreach (ITabContent tab in e.NewItems) Add(tab);
            break;
         case NotifyCollectionChangedAction.Remove:
            if (e.OldStartingIndex >= 0 && e.OldStartingIndex < Count) {
               RemoveAt(e.OldStartingIndex);
            } else {
               Resync();
            }
            break;
         default:
            // Move and Reset are rare enough that copying the editor's order is simplest.
            Resync();
            break;
      }
      // EditorViewModel.Add sets SelectedIndex *before* raising CollectionChanged, so by the time
      // the new tab exists here the TabControl has already coerced that index away. Tell the view
      // to re-apply it.
      Synced?.Invoke(this, EventArgs.Empty);
   }

   void Resync() {
      var tabs = new List<ITabContent>();
      foreach (var tab in editor) tabs.Add(tab);

      // Replace in place so the TabControl keeps its selection where it can.
      while (Count > tabs.Count) RemoveAt(Count - 1);
      for (int i = 0; i < tabs.Count; i++) {
         if (i < Count) {
            if (!ReferenceEquals(this[i], tabs[i])) this[i] = tabs[i];
         } else {
            Add(tabs[i]);
         }
      }
   }
}
