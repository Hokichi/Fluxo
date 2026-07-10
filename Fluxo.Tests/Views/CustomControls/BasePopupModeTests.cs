using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BasePopupModeTests
{
    [Fact]
    public void CanDiscard_DefaultsToTrue()
    {
        RunSta(() => Assert.True(new BasePopup().CanDiscard));
    }

    [Fact]
    public void Escape_ClosesWhenDiscardIsUnavailable()
    {
        RunSta(() =>
        {
            var popup = new RecordingPopup { Mode = PopupMode.SaveDiscard, CanDiscard = false };

            popup.InvokeShortcut(Key.Escape, ModifierKeys.None);

            Assert.Equal(0, popup.DiscardCount);
            Assert.Equal(1, popup.CloseCount);
        });
    }

    [Fact]
    public void Enter_InvokesSaveWhenSaveIsVisible()
    {
        RunSta(() =>
        {
            var popup = new RecordingPopup { Mode = PopupMode.SaveDiscard };

            popup.InvokeShortcut(Key.Enter, ModifierKeys.None);

            Assert.Equal(1, popup.SaveCount);
        });
    }

    [Fact]
    public void ShiftEnter_InvokesSaveAndNewOnlyWhenContinuationIsAvailable()
    {
        RunSta(() =>
        {
            var popup = new RecordingPopup { Mode = PopupMode.SaveDiscard, CanContinue = true };

            Assert.True(popup.InvokeShortcut(Key.Enter, ModifierKeys.Shift));
            Assert.Equal(1, popup.SaveAndNewCount);

            popup.CanContinue = false;
            Assert.False(popup.InvokeShortcut(Key.Enter, ModifierKeys.Shift));
            Assert.Equal(1, popup.SaveAndNewCount);
        });
    }

    [Fact]
    public void Escape_PrefersDiscardThenFallsBackToClose()
    {
        RunSta(() =>
        {
            var discardPopup = new RecordingPopup { Mode = PopupMode.SaveDiscard };
            discardPopup.InvokeShortcut(Key.Escape, ModifierKeys.None);

            Assert.Equal(1, discardPopup.DiscardCount);
            Assert.Equal(0, discardPopup.CloseCount);

            var closePopup = new RecordingPopup { Mode = PopupMode.Functional };
            closePopup.InvokeShortcut(Key.Escape, ModifierKeys.None);

            Assert.Equal(0, closePopup.DiscardCount);
            Assert.Equal(1, closePopup.CloseCount);
        });
    }

    [Fact]
    public void DefaultDiscard_UsesPopupCloseHandler()
    {
        RunSta(() =>
        {
            var popup = new DefaultDiscardPopup { Mode = PopupMode.SaveDiscard };

            popup.InvokeShortcut(Key.Escape, ModifierKeys.None);

            Assert.Equal(1, popup.CloseCount);
        });
    }

    [Fact]
    public void ApplyCancelAndBackNext_RoutePrimaryAndNavigationHotkeys()
    {
        RunSta(() =>
        {
            var applyPopup = new RecordingPopup { Mode = PopupMode.ApplyCancel };
            applyPopup.InvokeShortcut(Key.Enter, ModifierKeys.None);
            Assert.Equal(1, applyPopup.ApplyCount);

            var popup = new RecordingPopup { Mode = PopupMode.BackNext, CurrentStep = 1, StepCount = 2 };
            Assert.False(popup.InvokeShortcut(Key.Back, ModifierKeys.None));
            popup.InvokeShortcut(Key.Enter, ModifierKeys.None);
            Assert.Equal(1, popup.NextCount);

            popup.CurrentStep = 2;
            popup.InvokeShortcut(Key.Back, ModifierKeys.None);
            popup.InvokeShortcut(Key.Enter, ModifierKeys.None);
            Assert.Equal(1, popup.BackCount);
            Assert.Equal(1, popup.FinishCount);
        });
    }

    [Fact]
    public void Footer_MeasuresToContentWidth()
    {
        RunSta(() =>
        {
            EnsureApplicationResources();

            var popup = new BasePopup
            {
                Mode = PopupMode.SaveDiscard,
                Content = new Border { Width = 320, Height = 100 }
            };

            popup.Show();
            popup.UpdateLayout();

            Assert.InRange(popup.ActualWidth, 320, 600);
        });
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        foreach (var resource in new[]
                 {
                     "Theme.xaml", "Fonts.xaml", "Icons.xaml", "Converters.xaml",
                     "Styles/ContainerStyles.xaml", "Styles/ButtonStyles.xaml", "Styles/TextBoxStyles.xaml",
                     "Styles/GlobalStyles.xaml", "Styles/PopupStyles.xaml"
                 })
        {
            if (application.Resources.MergedDictionaries.Any(dictionary =>
                    dictionary.Source?.OriginalString.EndsWith($"Resources/{resource}", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/Fluxo.Resources;component/Resources/{resource}", UriKind.Relative)
            });
        }
    }

    private static void RunSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw new Xunit.Sdk.XunitException(exception.ToString());
    }

    private sealed class RecordingPopup : BasePopup
    {
        public int SaveCount { get; private set; }

        public int SaveAndNewCount { get; private set; }

        public int ApplyCount { get; private set; }

        public int DiscardCount { get; private set; }

        public int CloseCount { get; private set; }

        public int BackCount { get; private set; }

        public int NextCount { get; private set; }

        public int FinishCount { get; private set; }

        public bool InvokeShortcut(Key key, ModifierKeys modifiers) => TryHandlePopupShortcut(key, modifiers);

        protected override void OnSaveButtonClick() => SaveCount++;

        protected override void OnSaveAndCreateNewButtonClick() => SaveAndNewCount++;

        protected override void OnApplyButtonClick() => ApplyCount++;

        protected override void OnDiscardButtonClick() => DiscardCount++;

        protected override void OnCloseButtonClick() => CloseCount++;

        protected override void OnBackButtonClick() => BackCount++;

        protected override void OnNextButtonClick() => NextCount++;

        protected override void OnFinishButtonClick() => FinishCount++;
    }

    private sealed class DefaultDiscardPopup : BasePopup
    {
        public int CloseCount { get; private set; }

        public bool InvokeShortcut(Key key, ModifierKeys modifiers) => TryHandlePopupShortcut(key, modifiers);

        protected override void OnCloseButtonClick() => CloseCount++;
    }
}
