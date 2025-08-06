using CommunityToolkit.Mvvm.ComponentModel;
using DPUnity.Wpf.DpTreeView.Interfaces;
using System.Windows.Threading;

namespace DPUnity.Wpf.DpTreeView.FilterTreeViews
{
    internal partial class CheckIconFilterTreeViewViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private IHierarchicalCollectionView? _itemsSource;

        [ObservableProperty]
        private string _filterText = string.Empty;

        private readonly DispatcherTimer _filterUpdateTimer;

        public CheckIconFilterTreeViewViewModel()
        {
            _filterUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _filterUpdateTimer.Tick += (s, e) =>
            {
                _filterUpdateTimer.Stop();
                UpdateFilter();
            };
        }

        partial void OnItemsSourceChanged(IHierarchicalCollectionView? value)
        {
            UpdateFilter();
        }

        partial void OnFilterTextChanged(string value)
        {
            _filterUpdateTimer.Stop();
            _filterUpdateTimer.Start();
        }

        private void UpdateFilter()
        {
            if (ItemsSource == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(FilterText))
            {
                ItemsSource.Filter = null;
                ItemsSource.FilterString = string.Empty;
                ItemsSource.Refresh();
                return;
            }

            ItemsSource.FilterString = FilterText;
            ItemsSource.Filter = obj =>
            {
                if (obj is IHierarchicalItem item)
                {
                    return item.Text.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            };

            ItemsSource.Refresh();
        }

        public void Dispose()
        {
            _filterUpdateTimer.Stop();
            _filterUpdateTimer.Tick -= (s, e) => { };
        }
    }
}
