using DPUnity.Wpf.DpTreeView.Interfaces;
using System.Windows;
using System.Windows.Controls;

namespace DPUnity.Wpf.DpTreeView.FilterTreeViews
{
    /// <summary>
    /// Interaction logic for CheckIconFilterTreeView.xaml
    /// </summary>
    public partial class CheckIconFilterTreeView : UserControl
    {
        private readonly CheckIconFilterTreeViewViewModel _viewModel;

        public CheckIconFilterTreeView()
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
               typeof(CheckIconFilterTreeView),
               new PropertyMetadata(null, OnItemsSourceChanged));

        public IHierarchicalCollectionView ItemsSource
        {
            get { return (IHierarchicalCollectionView)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CheckIconFilterTreeView control)
            {
                control._viewModel.ItemsSource = e.NewValue as IHierarchicalCollectionView;
            }
        }
    }
}
