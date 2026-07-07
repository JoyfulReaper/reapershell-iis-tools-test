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
        if (args.Count == 0)
        {
            VersionInfoWriter.Write(context);
            return Task.FromResult(0);
        }

        if (args.Count == 1 && args[0] is "--help" or "-h" or "/?")
        {
            VersionInfoWriter.WriteUsage(context);
            return Task.FromResult(0);
        }

        context.WriteErrorLine($"Unknown option: {args[0]}");
        VersionInfoWriter.WriteUsage(context);
        return Task.FromResult(1);
    }
}
