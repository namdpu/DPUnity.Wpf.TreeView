using CommunityToolkit.Mvvm.ComponentModel;
using DPUnity.Wpf.DpTreeView.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Media;

namespace DPUnity.Wpf.FilterTreeView
{
    public abstract partial class HierarchicalItemBase<T> : ObservableObject, IHierarchicalItem, IDisposable, ITreeNode
        where T : HierarchicalItemBase<T>
    {
        [ObservableProperty]
        private bool _isMatch = true;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private bool _isExpanded = false;

        [ObservableProperty]
        private Brush? _foreground = null;

        [ObservableProperty]
        private double _fontSize = 12;

        /// <summary>
        /// This property is used to display text in the tree view.
        /// Override this property in derived classes to provide the text representation of the item.
        /// </summary>
        public virtual string Text => string.Empty;

        public ObservableCollection<T> Children { get; set; } = [];

        public T? Parent { get; private set; }

        protected HierarchicalItemBase()
        {
            IsMatch = true;
            IsVisible = true;
            IsExpanded = true;
            Children.CollectionChanged += OnChildrenChanged;
        }

        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }

        public void ExpandToLevel(int level)
        {
            int depth = GetDepth();
            IsExpanded = (depth < level - 1);
            foreach (var child in Children ?? Enumerable.Empty<T>())
            {
                child.ExpandToLevel(level);
            }
        }

        #region public Methods
        /// <summary>
        /// Gets the root item of the hierarchy.
        /// </summary>
        /// <returns>The root item.</returns>
        public T GetRoot()
        {
            T current = (T)this;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current;
        }

        /// <summary>
        /// Gets the ancestors of the current item in the hierarchy.
        /// </summary>
        /// <returns>An enumerable collection of ancestor items.</returns>
        public IEnumerable<T> GetAncestors()
        {
            T current = (T)this;
            while (current.Parent != null)
            {
                yield return current.Parent;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Checks if the current item is a descendant of the specified item.
        /// </summary>
        /// <param name="other">The item to check against.</param>
        /// <returns>True if the current item is a descendant of the specified item; otherwise, false.</returns>
        public bool IsDescendantOf(T other)
        {
            if (other == null) return false;
            T current = (T)this;
            while (current.Parent != null)
            {
                if (current.Parent == other)
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Gets the depth of the current item in the hierarchy.
        /// </summary>
        /// <returns>The depth of the item.</returns>
        public int GetDepth()
        {
            int depth = 0;
            T current = (T)this;
            while (current.Parent != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }

        /// <summary>
        /// Gets the path from the current item to the root of the hierarchy.
        /// </summary>
        /// <returns>A collection of items representing the path from the current item to the root.</returns>
        public IEnumerable<T> GetPathFromRoot()
        {
            var path = new List<T>();
            T? current = (T)this;
            while (current != null)
            {
                path.Insert(0, current);
                current = current.Parent;
            }
            return path;
        }
        #endregion

        private void OnChildrenChanged(object? sender,
                                   NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (T child in e.NewItems)
                {
                    child.Parent = (T)this;
                }
            }
            if (e.OldItems != null)
            {
                foreach (T child in e.OldItems)
                {
                    if (child.Parent == this)
                        child.Parent = null;
                }
            }
        }

        public void Dispose()
        {
            foreach (var child in Children)
            {
                child.Dispose();
            }
        }
    }
}