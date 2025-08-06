using DPUnity.Wpf.DpTreeView.Interfaces;
using DPUnity.Wpf.FilterTreeView.FilterTreeView;
using System.Windows;
using System.Windows.Controls;

namespace DPUnity.Wpf.DpTreeView.FilterTreeViews
{
    /// <summary>
    /// Interaction logic for FilterTreeView.xaml
    /// </summary>
    public partial class FilterTreeView : UserControl
    {
        private readonly FilterTreeViewViewModel _viewModel;

        public FilterTreeView()
        {
            InitializeComponent();
            _viewModel = new();
            DataContext = _viewModel;
            ResourceDictionary rd = new()
            {
                Source = new Uri("/DPUnity.Wpf.DpTreeView;component/TreeViewStyle.xaml", UriKind.Relative)
            };
            Resources.MergedDictionaries.Add(rd);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                "ItemsSource",
                typeof(IHierarchicalCollectionView),
                typeof(FilterTreeView),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IHierarchicalCollectionView ItemsSource
        {
            get { return (IHierarchicalCollectionView)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterTreeView control)
            {
                control._viewModel.ItemsSource = e.NewValue as IHierarchicalCollectionView;
            }
        }
    }
}