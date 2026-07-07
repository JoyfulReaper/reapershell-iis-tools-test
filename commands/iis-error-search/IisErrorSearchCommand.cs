using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public sealed class IisErrorSearchCommand : IShellCommand
{
    public string Name => "iis-error-search";

    public string Description => "Search app/stdout logs and IIS W3C logs for error signals.";

    public Task<int> ExecuteAsync(
        ShellContext context,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        var parser = new OptionParser();
        if (!parser.TryParse(context, args, out var options))
        {
            return Task.FromResult(1);
        }

        if (options.ShowHelp)
        {
            CommandUsage.Write(context);
            return Task.FromResult(0);
        }

        var discovery = new LogFileDiscovery(options, context.WorkingDirectory.FullName);
        var appFiles = discovery.DiscoverAppLogFiles();
        var iisFiles = discovery.DiscoverIisLogFiles();

        if (options.NewestFilesOnly)
        {
            appFiles = appFiles.Take(options.NewestFileCount).ToList();
            iisFiles = iisFiles.Take(options.NewestFileCount).ToList();
        }

        context.WriteLine($"App log files: {appFiles.Count}");
        context.WriteLine($"IIS log files: {iisFiles.Count}");
        context.WriteLine($"Showing newest {options.Last} result(s).");

        var appSearcher = new AppLogSearcher(context);
        var iisSearcher = new IisW3cLogSearcher(context);

        var appMatches = appSearcher.Search(appFiles, options.AppPatterns, options.Last, cancellationToken);
        OutputWriter.WriteAppMatches(context, appMatches);

        var iisMatches = iisSearcher.Search(iisFiles, options.IisStatusCodes, options.Last, cancellationToken);
        OutputWriter.WriteIisMatches(context, iisMatches);
        OutputWriter.WriteSummary(context, iisMatches);

        return Task.FromResult(0);
    }
}
