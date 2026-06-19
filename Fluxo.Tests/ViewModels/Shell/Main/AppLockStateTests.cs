using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Shell.Main;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public sealed class AppLockStateTests
{
    [Fact]
    public void ApplySettings_LoadsAppLockSettings()
    {
        var state = new AppLockState(new TestPasswordProtector());

        state.ApplySettings(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UserSettingNames.IsAppAutoLocked] = bool.TrueString,
            [UserSettingNames.AppAutoLockedInterval] = "120",
            [UserSettingNames.UILockingPassword] = "protected:secret-pass"
        });

        Assert.True(state.IsAppAutoLocked);
        Assert.Equal(120, state.AppAutoLockedInterval);
        Assert.True(state.HasUiLockingPassword);
    }

    [Fact]
    public void LockUi_SetsLockedState()
    {
        var state = new AppLockState(new TestPasswordProtector());

        state.LockUi();

        Assert.True(state.IsAppLocked);
    }

    [Fact]
    public void TryUnlockUi_ReturnsTrue_WhenNoPasswordSaved()
    {
        var state = new AppLockState(new TestPasswordProtector());
        state.LockUi();

        var unlocked = state.TryUnlockUi(null);

        Assert.True(unlocked);
        Assert.False(state.IsAppLocked);
    }

    [Fact]
    public void TryUnlockUi_ReturnsFalse_WhenPasswordMismatch()
    {
        var state = new AppLockState(new TestPasswordProtector());
        state.ApplySettings(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UserSettingNames.UILockingPassword] = "protected:secret-pass"
        });
        state.LockUi();

        var unlocked = state.TryUnlockUi("wrong");

        Assert.False(unlocked);
        Assert.True(state.IsAppLocked);
    }

    [Fact]
    public void TryUnlockUi_ReturnsTrue_WhenPasswordMatches()
    {
        var state = new AppLockState(new TestPasswordProtector());
        state.ApplySettings(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UserSettingNames.UILockingPassword] = "protected:secret-pass"
        });
        state.LockUi();

        var unlocked = state.TryUnlockUi("secret-pass");

        Assert.True(unlocked);
        Assert.False(state.IsAppLocked);
    }

    private sealed class TestPasswordProtector : IUiLockPasswordProtector
    {
        public string Protect(string? password)
        {
            return string.IsNullOrWhiteSpace(password) ? string.Empty : "protected:" + password;
        }

        public string Unprotect(string? protectedPassword)
        {
            if (string.IsNullOrWhiteSpace(protectedPassword))
                return string.Empty;

            return protectedPassword.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedPassword["protected:".Length..]
                : string.Empty;
        }
    }
}
