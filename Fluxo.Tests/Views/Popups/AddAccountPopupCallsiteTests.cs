using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddAccountPopupCallsiteTests
{
    [Fact]
    public void AddAccountPopup_EnablesSaveAndCreateNew()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("ShowSaveAndCreateNewButton=\"True\"", xaml);
        Assert.Contains("IsSaveAndCreateNewButtonEnabled=\"{Binding CanSave}\"", xaml);
    }

    [Fact]
    public void AddAccountPopup_ShowsFieldValidationHints()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("Text=\"{Binding NameValidationHint}\"", xaml);
        Assert.Contains("Text=\"{Binding MaximumSpendingValidationHint}\"", xaml);
        Assert.Contains("Text=\"{Binding SpentAmountValidationHint}\"", xaml);
        Assert.Contains("Text=\"{Binding MinimumPaymentValidationHint}\"", xaml);
        Assert.Contains("Text=\"{Binding ApyValidationHint}\"", xaml);
    }

    [Fact]
    public void AddAccountPopup_FieldValidationHeaders_MatchTransactionPopupSpacingPattern()
    {
        var xaml = ReadPopupXaml();
        var compactXaml = RemoveWhitespace(xaml);

        Assert.DoesNotContain("ValidatesOnNotifyDataErrors=True", xaml);
        Assert.Contains(RemoveWhitespace("Margin=\"0\" Style=\"{StaticResource FieldLabelStyle}\" Text=\"Maximum Spending\""), compactXaml);
        Assert.Contains(RemoveWhitespace("Margin=\"0\" Style=\"{StaticResource FieldLabelStyle}\" Text=\"Minimum Payment\""), compactXaml);
        Assert.Contains(RemoveWhitespace("Margin=\"0\" Style=\"{StaticResource FieldLabelStyle}\" Text=\"APY\""), compactXaml);
    }

    [Fact]
    public void AddAccountPopup_ValidatesFieldsOnlyOnLostKeyboardFocus()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("LostKeyboardFocus=\"OnNameTextBoxLostKeyboardFocus\"", xaml);
        Assert.Contains("LostKeyboardFocus=\"OnMaximumSpendingTextBoxLostKeyboardFocus\"", xaml);
        Assert.Contains("LostKeyboardFocus=\"OnSpentAmountTextBoxLostKeyboardFocus\"", xaml);
        Assert.Contains("LostKeyboardFocus=\"OnMinimumPaymentTextBoxLostKeyboardFocus\"", xaml);
        Assert.Contains("LostKeyboardFocus=\"OnApyTextBoxLostKeyboardFocus\"", xaml);
    }

    [Fact]
    public void AddAccountPopup_BindsMaximumSpendingPlaceholder()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("PlaceholderText=\"{Binding MaximumSpendingPlaceholderText}\"", xaml);
        Assert.Contains("TextChanged=\"OnMaximumSpendingTextBoxTextChanged\"", xaml);
    }

    [Fact]
    public void AddAccountPopup_SaveFailure_UsesFloatingNotification()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AddAccountPopup.xaml.cs"));

        Assert.Contains("FloatingNotificationPublisher.SaveFailed([message]);", source);
        Assert.DoesNotContain("ShowWarningToast(result.ErrorMessage);", source);
    }

    [Fact]
    public void AddAccountPopup_SaveAndCreateNew_ResetsViewModelAndKeepsPopupOpen()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AddAccountPopup.xaml.cs"));

        Assert.Contains("protected override async void OnSaveAndCreateNewButtonClick()", source);
        Assert.Contains("_viewModel.ResetAfterSaveAndCreateNew();", source);
        Assert.Contains("FocusPrimaryInput();", source);
    }

    private static string ReadPopupXaml()
    {
        return File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AddAccountPopup.xaml"));
    }

    private static string RemoveWhitespace(string value)
    {
        return string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
    }

    private static string GetRepositoryRootPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
