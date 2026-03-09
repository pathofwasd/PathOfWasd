using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PathOfWASD.Overlays.BGFunctionalities;

public class FilteredKeyPicker<T> : ObservableObject where T : Enum
{
    private readonly IList<T> _allItems;
    private readonly Func<IEnumerable<T>> _getExcludedItems;

    public ICollectionView FilteredItems { get; }

    public FilteredKeyPicker(
        IEnumerable<T> sourceItems,
        Func<IEnumerable<T>> getExcludedItems)
    {
        _allItems         = sourceItems.ToList();
        _getExcludedItems = getExcludedItems;
        FilteredItems     = CollectionViewSource.GetDefaultView(_allItems);
        FilteredItems.Filter = FilterPredicate;
    }

    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                FilteredItems.Refresh();
        }
    }

    private T _selectedKey;
    public T SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                FilteredItems.Refresh();
            }
        }
    }

    private bool FilterPredicate(object item)
    {
        if (item is not T enumItem) return false;

        if (enumItem.Equals(SelectedKey))
            return true;

        if (!string.IsNullOrWhiteSpace(FilterText) &&
            enumItem.ToString().IndexOf(FilterText, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return !_getExcludedItems().Contains(enumItem);
    }
}