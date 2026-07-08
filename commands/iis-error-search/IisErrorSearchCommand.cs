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

        if (options.ShowVersion)
        {
            var versionCurse = CursedShellIntegration.TryGet(context);
            if (versionCurse?.IsEnabled == true)
            {
                versionCurse.AddAmbientEvent("iis-tools-version revealed the loaded DLL's true name.");
                versionCurse.AddAmbientEvent("The curse compares branch metadata with theatrical suspicion.");
            }

            VersionInfoWriter.Write(context, versionCurse);
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
        var curse = CursedShellIntegration.TryGet(context);
        var isCursed = curse?.IsEnabled == true;

        if (isCursed)
        {
            curse!.AddAmbientEvent("iis-error-search opened the log crypt.");
            curse.AddAmbientEvent("The IIS logs rustle like something with too many status codes.");
            curse.AddAmbientEvent("A daemon checks the W3C fields and pretends this is normal.");
        }

        if (options.NewestFilesOnly)
        {
            appFiles = appFiles.Take(options.NewestFileCount).ToList();
            iisFiles = iisFiles.Take(options.NewestFileCount).ToList();
        }

        IisErrorSearchRenderer.WriteRunHeader(context, options, appFiles.Count, iisFiles.Count);
        IisErrorSearchRenderer.WriteIisFileHintIfNeeded(context, iisFiles, options.IisLogPaths);

        if (isCursed && options.IisLogPaths.Any(IisErrorSearchRenderer.IsSuspiciousGlob))
        {
            curse!.AddAmbientEvent("The curse points at --iis-log and whispers: that is a path filter, not a bot detector.");
        }

        var appSearcher = new AppLogSearcher();
        var appMatches = appSearcher.Search(
            appFiles,
            options.AppPatterns,
            options.Last,
            options.DisplayOrder,
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

        if (isCursed)
        {
            ApplyCursedOutcome(curse!, options, appMatches, iisMatches);
        }

        if (options.FailOnMatch && (appMatches.Count > 0 || iisMatches.Count > 0))
        {
            return Task.FromResult(2);
        }

        return Task.FromResult(0);
    }

    private static void ApplyCursedOutcome(
        ReaperShell.Abstractions.ICursedShell curse,
        IisErrorSearchOptions options,
        IReadOnlyCollection<AppMatch> appMatches,
        IReadOnlyCollection<IisMatch> iisMatches)
    {
        if (appMatches.Count == 0 && iisMatches.Count == 0)
        {
            curse.AddAmbientEvent("The logs are quiet. Too quiet.");
            curse.AddAmbientEvent("No errors found. The curse distrusts this.");
            return;
        }

        curse.AddAmbientEvent("The logs confessed. The curse is pleased.");

        if (HasIis5xxMatches(iisMatches))
        {
            curse.ShiftMood("hungry");
            curse.AddAmbientEvent("IIS produced evidence. The shell writes it down in red ink.");
        }
        else if (HasOnlyIis4xxMatches(iisMatches))
        {
            curse.ShiftMood("petty");
        }

        if (HasBotLikeFilter(options))
        {
            curse.ShiftMood("suspicious");
            curse.AddAmbientEvent("Bot traffic detected. The curse narrows its eyes.");
        }

        if (HasExceptionLikeAppMatches(appMatches))
        {
            curse.AddAmbientEvent("A stack trace crawls out of stdout and asks for a name.");
        }
    }

    private static bool HasIis5xxMatches(IReadOnlyCollection<IisMatch> matches)
    {
        return matches.Any(match => match.Status >= 500);
    }

    private static bool HasOnlyIis4xxMatches(IReadOnlyCollection<IisMatch> matches)
    {
        return matches.Count > 0 && matches.All(match => match.Status >= 400 && match.Status < 500);
    }

    private static bool HasBotLikeFilter(IisErrorSearchOptions options)
    {
        return ContainsBotToken(options.IisContainsPatterns) ||
               ContainsBotToken(options.UserAgentPatterns) ||
               ContainsBotToken(options.UrlPatterns);
    }

    private static bool ContainsBotToken(IEnumerable<string> values)
    {
        return values.Any(value => value.Contains("bot", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExceptionLikeAppMatches(IReadOnlyCollection<AppMatch> matches)
    {
        return matches.Any(match =>
            match.Line.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
            match.Line.Contains("stacktrace", StringComparison.OrdinalIgnoreCase) ||
            match.Line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            match.Line.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
            match.Line.Contains("fail", StringComparison.OrdinalIgnoreCase));
    }
}
