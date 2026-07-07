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
        var parser = new IisErrorSearchOptionsParser();
        if (!parser.TryParse(context, args, out var options))
        {
            return Task.FromResult(1);
        }

        if (options.ShowHelp)
        {
            IisErrorSearchRenderer.WriteUsage(context);
            return Task.FromResult(0);
        }

        Action<string>? warningWriter = null;
        if (options.Verbose)
        {
            warningWriter = message => IisErrorSearchRenderer.WriteWarning(context, message);
        }

        var finder = new LogFileFinder(options, context.WorkingDirectory.FullName, options.Verbose, warningWriter);
        var appFiles = finder.DiscoverAppLogFiles();
        var iisFiles = finder.DiscoverIisLogFiles();

        if (options.NewestFilesOnly)
        {
            appFiles = appFiles.Take(options.NewestFileCount).ToList();
            iisFiles = iisFiles.Take(options.NewestFileCount).ToList();
        }

        IisErrorSearchRenderer.WriteRunHeader(context, options, appFiles.Count, iisFiles.Count);
        IisErrorSearchRenderer.WriteIisFileHintIfNeeded(context, iisFiles, options.IisLogPaths);

        var appSearcher = new AppLogSearcher(context);
        var appMatches = appSearcher.Search(
            appFiles,
            options.AppPatterns,
            options.Last,
            options.SinceUtc,
            options.Verbose,
            warningWriter,
            cancellationToken);

        IisErrorSearchRenderer.WriteAppMatches(context, appMatches);

        var iisSearcher = new IisW3cLogSearcher(new IisW3cParser());
        var iisMatches = iisSearcher.Search(
            iisFiles,
            options,
            warningWriter,
            cancellationToken);

        IisErrorSearchRenderer.WriteIisMatches(context, iisMatches);
        IisErrorSearchRenderer.WriteSummary(context, iisMatches);

        if (options.FailOnMatch && (appMatches.Count > 0 || iisMatches.Count > 0))
        {
            return Task.FromResult(2);
        }

        return Task.FromResult(0);
    }
}
