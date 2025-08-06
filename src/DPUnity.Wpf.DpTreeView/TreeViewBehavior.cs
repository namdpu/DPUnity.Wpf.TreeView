using DPUnity.Wpf.DpTreeView.Interfaces;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DPUnity.Wpf.DpTreeView
{
    public static class TreeViewBehavior
    {
        #region Dependency Properties
        // Expand/Collapse related properties
        public static readonly DependencyProperty ExpandAllCommandProperty =
            DependencyProperty.RegisterAttached(
                "ExpandAllCommand",
                typeof(ICommand),
                typeof(TreeViewBehavior),
                new PropertyMetadata(new ExpandAllCommand()));

        public static void SetExpandAllCommand(DependencyObject element, ICommand value)
            => element.SetValue(ExpandAllCommandProperty, value);

        public static ICommand GetExpandAllCommand(DependencyObject element)
            => (ICommand)element.GetValue(ExpandAllCommandProperty);

        public static readonly DependencyProperty CollapseAllCommandProperty =
            DependencyProperty.RegisterAttached(
                "CollapseAllCommand",
                typeof(ICommand),
                typeof(TreeViewBehavior),
                new PropertyMetadata(new CollapseAllCommand()));

        public static void SetCollapseAllCommand(DependencyObject element, ICommand value)
            => element.SetValue(CollapseAllCommandProperty, value);

        public static ICommand GetCollapseAllCommand(DependencyObject element)
            => (ICommand)element.GetValue(CollapseAllCommandProperty);

        public static readonly DependencyProperty ExpandLevelsProperty =
            DependencyProperty.RegisterAttached(
                "ExpandLevels",
                typeof(IEnumerable<int>),
                typeof(TreeViewBehavior),
                new PropertyMetadata(null));

        public static IEnumerable<int> GetExpandLevels(DependencyObject obj)
        {
            return (IEnumerable<int>)obj.GetValue(ExpandLevelsProperty);
        }

        public static void SetExpandLevels(DependencyObject obj, IEnumerable<int> value)
        {
            obj.SetValue(ExpandLevelsProperty, value);
        }

        public static readonly DependencyProperty SelectedExpandLevelProperty =
            DependencyProperty.RegisterAttached(
                "SelectedExpandLevel",
                typeof(int),
                typeof(TreeViewBehavior),
                new PropertyMetadata(0, OnSelectedExpandLevelChanged));

        public static int GetSelectedExpandLevel(DependencyObject obj)
        {
            return (int)obj.GetValue(SelectedExpandLevelProperty);
        }

        public static void SetSelectedExpandLevel(DependencyObject obj, int value)
        {
            obj.SetValue(SelectedExpandLevelProperty, value);
        }

        // ItemsSource and Behavior enabling properties
        public static readonly DependencyProperty ItemsSourceWatcherProperty =
            DependencyProperty.RegisterAttached(
                "ItemsSourceWatcher",
                typeof(object),
                typeof(TreeViewBehavior),
                new PropertyMetadata(null, OnItemsSourceWatcherChanged));

        public static object GetItemsSourceWatcher(DependencyObject obj)
        {
            return obj.GetValue(ItemsSourceWatcherProperty);
        }

        public static void SetItemsSourceWatcher(DependencyObject obj, object value)
        {
            obj.SetValue(ItemsSourceWatcherProperty, value);
        }

        public static readonly DependencyProperty IsBehaviorEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsBehaviorEnabled",
                typeof(bool),
                typeof(TreeViewBehavior),
                new PropertyMetadata(false, OnIsBehaviorEnabledChanged));

        public static bool GetIsBehaviorEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsBehaviorEnabledProperty);
        }

        public static void SetIsBehaviorEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsBehaviorEnabledProperty, value);
        }

        // Internal state property
        public static readonly DependencyProperty IsUpdatingExpandLevelProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdatingExpandLevel",
                typeof(bool),
                typeof(TreeViewBehavior),
                new PropertyMetadata(false));

        public static bool GetIsUpdatingExpandLevel(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsUpdatingExpandLevelProperty);
        }

        public static void SetIsUpdatingExpandLevel(DependencyObject obj, bool value)
        {
            obj.SetValue(IsUpdatingExpandLevelProperty, value);
        }

        #endregion

        #region Command Classes
        public class ExpandAllCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return parameter is TreeView;
            }

            public void Execute(object? parameter)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                if (parameter is TreeView treeView && treeView.ItemsSource is IHierarchicalCollectionView hierarchicalView)
                {
                    SetIsUpdatingExpandLevel(treeView, true);
                    foreach (var item in treeView.Items)
                    {
                        if (item is ITreeNode treeNode)
                        {
                            treeNode.ExpandAll();
                        }
                    }
                    int maxDepth = hierarchicalView.GetMaxDepth();
                    SetSelectedExpandLevel(treeView, maxDepth);
                    SetIsUpdatingExpandLevel(treeView, false);
                }
                Mouse.OverrideCursor = null;
            }
        }

        public class CollapseAllCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return parameter is TreeView;
            }

            public void Execute(object? parameter)
            {
                Mouse.OverrideCursor = Cursors.Wait;
                if (parameter is TreeView treeView && treeView.ItemsSource is IHierarchicalCollectionView)
                {
                    SetIsUpdatingExpandLevel(treeView, true);
                    foreach (var item in treeView.Items)
                    {
                        if (item is ITreeNode treeNode)
                        {
                            treeNode.CollapseAll();
                        }
                    }
                    SetSelectedExpandLevel(treeView, 1);
                    SetIsUpdatingExpandLevel(treeView, false);
                }
                Mouse.OverrideCursor = null;
            }
        }

        #endregion

        #region Helper Methods
        public static void Attach(TreeView treeView)
        {
            DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(TreeView))
                .AddValueChanged(treeView, (s, e) => UpdateExpandLevels(treeView));
            treeView.Loaded += (s, e) => UpdateExpandLevels(treeView);
            // Ensure initial update
            UpdateExpandLevels(treeView);
        }

        private static void UpdateExpandLevels(TreeView treeView)
        {
            if (treeView.ItemsSource is IHierarchicalCollectionView hierarchicalView)
            {
                int maxDepth = hierarchicalView.GetMaxDepth();
                SetExpandLevels(treeView, Enumerable.Range(1, maxDepth));
            }
        }

        private static void OnSelectedExpandLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            if (GetIsUpdatingExpandLevel(d)) return;

            if (d is TreeView treeView && treeView.ItemsSource is IHierarchicalCollectionView hierarchicalView)
            {
                hierarchicalView.ExpandToLevel((int)e.NewValue);
            }
            Mouse.OverrideCursor = null;
        }

        private static void OnItemsSourceWatcherChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView treeView)
            {
                UpdateExpandLevels(treeView);
            }
        }

        private static void OnIsBehaviorEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView treeView && (bool)e.NewValue)
            {
                Attach(treeView);
            }
        }

        #endregion
    }
}
