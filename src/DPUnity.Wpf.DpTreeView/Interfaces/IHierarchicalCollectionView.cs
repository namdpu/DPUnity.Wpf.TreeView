using System.ComponentModel;

namespace DPUnity.Wpf.DpTreeView.Interfaces
{
    public interface IHierarchicalCollectionView : ICollectionView
    {
        new Predicate<object>? Filter { get; set; }
        IHierarchicalCollectionView GetChildView(object parent);
        void RefreshTree();
        string FilterString { get; set; }
        int GetMaxDepth();
        void ExpandToLevel(int level);
    }
}
