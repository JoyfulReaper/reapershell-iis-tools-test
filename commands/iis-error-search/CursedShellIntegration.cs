using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

internal static class CursedShellIntegration
{
    public static ICursedShell? TryGet(ShellContext context)
    {
        return context.Services?.GetService(typeof(ICursedShell)) as ICursedShell;
    }
}
