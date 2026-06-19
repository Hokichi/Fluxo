namespace Fluxo.Core.Interfaces.Services;

public interface IUiLockPasswordProtector
{
    string Protect(string? password);

    string Unprotect(string? protectedPassword);
}

