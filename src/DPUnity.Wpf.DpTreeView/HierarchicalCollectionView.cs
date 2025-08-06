using DPUnity.Wpf.DpTreeView.Interfaces;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace DPUnity.Wpf.DpTreeView
{
    public sealed class HierarchicalCollectionView<T> : CollectionView, IHierarchicalCollectionView
    {
        private readonly Func<T, IEnumerable<T>> _childrenSelector;
        private Predicate<object>? _filter;
        private readonly IEnumerable<T> _rootItems;
        public string FilterString { get; set; } = string.Empty;

        public HierarchicalCollectionView(IEnumerable<T> root, Func<T, IEnumerable<T>> childrenSelector)
            : base(new ObservableCollection<T>(root))
        {
            _rootItems = root;
            _childrenSelector = childrenSelector ?? throw new ArgumentNullException(nameof(childrenSelector));
        }

        public new Predicate<object>? Filter
        {
            get => _filter;
            set
            {
                if (_filter != value)
                {
                    _filter = value;
                    RefreshTree();
                }
            }
        }

        public IHierarchicalCollectionView GetChildView(object parent)
        {
            if (parent is T typedParent)
            {
                var children = _childrenSelector(typedParent);
                var childView = new HierarchicalCollectionView<T>(children, _childrenSelector)
                {
                    Filter = Filter // Kế thừa cùng predicate
                };
                return childView;
            }

            return new HierarchicalCollectionView<T>([], _childrenSelector);
        }

        public void RefreshTree()
        {
            using (DeferRefresh())
            {
                foreach (var item in _rootItems)
                {
                    UpdateItemVisibility(item, false);
                }
            }
        }

        private bool UpdateItemVisibility(T item, bool ancestorMatches)
        {
            if (item is IHierarchicalItem hierarchicalItem)
            {
                bool isMatch = Filter?.Invoke(item) ?? true;
                hierarchicalItem.IsMatch = isMatch;

                bool hasMatchesInSubtree = false;
                var children = _childrenSelector(item);
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        bool childHasMatches = UpdateItemVisibility(child, ancestorMatches || isMatch);
                        if (childHasMatches)
                        {
                            hasMatchesInSubtree = true;
                        }
                    }
                }

                hierarchicalItem.IsVisible = isMatch || ancestorMatches || hasMatchesInSubtree;
                if (Filter != null)
                {
                    hierarchicalItem.IsExpanded = hasMatchesInSubtree;
                }

                return isMatch || hasMatchesInSubtree;
            }
            else
            {
                return Filter?.Invoke(item ?? new object()) ?? true;
            }
        }

        public override bool PassesFilter(object item)
        {
            if (item is IHierarchicalItem hierarchicalItem)
            {
                return hierarchicalItem.IsVisible;
            }

            // Fallback cho items không implement IHierarchicalItem
            return Filter?.Invoke(item) ?? true;
        }

        protected override void RefreshOverride()
        {
            foreach (var item in _rootItems)
            {
                UpdateItemVisibility(item, false);
            }
            base.RefreshOverride();
        }

        public int GetMaxDepth()
        {
            return CalculateMaxDepth(_rootItems, _childrenSelector, 0);
        }

        public void ExpandToLevel(int level)
        {
            foreach (var item in _rootItems ?? Enumerable.Empty<T>())
            {
                if (item is IHierarchicalItem)
                {
                    SetExpandedStateByLevel(item, level, 0);
                }
            }
            Refresh();
        }

        private void SetExpandedStateByLevel(T item, int targetLevel, int currentLevel)
        {
            if (item is IHierarchicalItem hierarchicalItem)
            {
                hierarchicalItem.IsExpanded = currentLevel < targetLevel - 1;

                var children = _childrenSelector(item);
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        SetExpandedStateByLevel(child, targetLevel, currentLevel + 1);
                    }
                }
            }
        }

        private static int CalculateMaxDepth(IEnumerable<T> items, Func<T, IEnumerable<T>> childrenSelector, int currentDepth)
        {
            int maxDepth = currentDepth;
            foreach (var item in items ?? [])
            {
                var children = childrenSelector(item);
                if (children != null)
                {
                    int childMaxDepth = HierarchicalCollectionView<T>.CalculateMaxDepth(children, childrenSelector, currentDepth + 1);
                    if (childMaxDepth > maxDepth)
                    {
                        maxDepth = childMaxDepth;
                    }
                }
            }
            return maxDepth;
        }
    }

    public static class HierarchicalCollectionViewExtensions
    {
        public static IHierarchicalCollectionView ToHierarchicalView<T>(
            this IEnumerable<T> source,
            Func<T, IEnumerable<T>> childrenSelector)
        {
            return new HierarchicalCollectionView<T>(source, childrenSelector);
        }
    }
}