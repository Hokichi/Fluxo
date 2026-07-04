using Fluxo.Core.Entities;

namespace Fluxo.Views.Popups;

public partial class IoUAccountSelectionPopup : BasePopup
{
    public IoUAccountSelectionPopup(IReadOnlyList<Account> accounts)
    {
        InitializeComponent();
        AccountComboBox.ItemsSource = accounts;
        AccountComboBox.SelectedItem = accounts.FirstOrDefault(account => account.IsDefault) ?? accounts.FirstOrDefault();
        IsSaveButtonEnabled = AccountComboBox.SelectedItem is not null;
        AccountComboBox.SelectionChanged += (_, _) =>
            IsSaveButtonEnabled = AccountComboBox.SelectedItem is not null;
    }

    public int? SelectedAccountId { get; private set; }

    protected override void OnSaveButtonClick()
    {
        if (AccountComboBox.SelectedItem is not Account account)
            return;

        SelectedAccountId = account.Id;
        DialogResult = true;
    }
}
