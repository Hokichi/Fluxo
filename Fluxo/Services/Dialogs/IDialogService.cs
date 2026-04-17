using System.Windows;

namespace Fluxo.Services.Dialogs;

public interface IDialogService
{
    TPopup GetPopupOfType<TPopup>() where TPopup : class;

    bool? ShowPopupOfType<TPopup>(Window? owner = null) where TPopup : Window;

    MessageBoxResult ShowWarning(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowError(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowInformation(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.OK);

    MessageBoxResult ShowQuestion(string message, string title, Window? owner = null,
        MessageBoxButton buttons = MessageBoxButton.YesNo);
}
