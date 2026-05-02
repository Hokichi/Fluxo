using System.Reflection;
using System.Windows;

namespace Fluxo.Resources.Infrastructure;

internal static class WindowMethodInvoker
{
    public static bool TryInvoke(FrameworkElement source, string methodName, object? argument = null)
    {
        if (!TryResolveTarget(source, methodName, argument, out var window, out var method, out var args))
            return false;

        method!.Invoke(window, args);
        return true;
    }

    public static async Task<bool> TryInvokeAsync(FrameworkElement source, string methodName, object? argument = null)
    {
        if (!TryResolveTarget(source, methodName, argument, out var window, out var method, out var args))
            return false;

        var result = method!.Invoke(window, args);
        if (result is Task task)
            await task.ConfigureAwait(true);

        return true;
    }

    private static bool TryResolveTarget(
        FrameworkElement source,
        string methodName,
        object? argument,
        out Window? window,
        out MethodInfo? method,
        out object?[]? args)
    {
        window = Window.GetWindow(source);
        method = null;
        args = null;

        if (window is null)
            return false;

        if (argument is null)
        {
            method = window.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);

            if (method is null)
                return false;

            args = Array.Empty<object>();
            return true;
        }

        method = window.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(candidate =>
            {
                if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    return false;

                var parameters = candidate.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(argument);
            });

        if (method is null)
            return false;

        args = new[] { argument };
        return true;
    }
}
