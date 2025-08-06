namespace DPUnity.Wpf.DpTreeView.Interfaces
{
    public interface IHierarchicalItem
    {
        bool IsMatch { get; set; }
        bool IsVisible { get; set; }
        bool IsExpanded { get; set; }
        string Text { get; }
    }
}
