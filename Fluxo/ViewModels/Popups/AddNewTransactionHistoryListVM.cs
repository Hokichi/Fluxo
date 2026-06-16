using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fluxo.ViewModels.Popups;

public sealed partial class AddNewTransactionHistoryListVM : ObservableObject
{
    private readonly int _pageSize;
    private IReadOnlyList<AddNewTransactionHistoryItemVM> _source = [];
    private int _loadedCount;

    public AddNewTransactionHistoryListVM(int pageSize = 20)
    {
        _pageSize = Math.Max(1, pageSize);
        LoadMoreCommand = new RelayCommand(LoadMore, () => HasMoreItems);
    }

    public ObservableCollection<AddNewTransactionHistoryItemVM> Items { get; } = [];

    public IRelayCommand LoadMoreCommand { get; }

    public bool HasMoreItems => _loadedCount < _source.Count;
    public bool IsEmpty => Items.Count == 0;

    public void Reset(IReadOnlyList<AddNewTransactionHistoryItemVM> source)
    {
        _source = source;
        _loadedCount = 0;
        Items.Clear();
        LoadMore();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void LoadMore()
    {
        if (!HasMoreItems)
            return;

        var nextItems = _source
            .Skip(_loadedCount)
            .Take(_pageSize)
            .ToList();

        foreach (var item in nextItems)
            Items.Add(item);

        _loadedCount += nextItems.Count;
        OnPropertyChanged(nameof(HasMoreItems));
        OnPropertyChanged(nameof(IsEmpty));
        LoadMoreCommand.NotifyCanExecuteChanged();
    }
}
