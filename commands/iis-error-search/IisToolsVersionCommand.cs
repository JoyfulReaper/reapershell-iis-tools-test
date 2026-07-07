using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class IisToolsVersionCommand : IShellCommand
{
    public string Name => "iis-tools-version";

    public string Description => "Show build metadata for the loaded IIS tools command pack.";

    public Task<int> ExecuteAsync(
        ShellContext context,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        VersionInfoWriter.Write(context);
        return Task.FromResult(0);
    }
}
