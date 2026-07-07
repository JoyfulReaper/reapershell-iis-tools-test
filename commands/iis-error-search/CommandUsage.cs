using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public static class CommandUsage
{
    public static void Write(ShellContext context)
    {
        context.WriteLine("Usage:");
        context.WriteLine("  iis-error-search [options]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  --app-log <glob>           App/stdout log glob. Can be repeated.");
        context.WriteLine("  --iis-log <glob>           IIS W3C log glob. Can be repeated.");
        context.WriteLine("  --pattern <text>           App/stdout search pattern. Can be repeated.");
        context.WriteLine("  --status <code[,code]>     IIS status code filter. Can be repeated.");
        context.WriteLine("  --last <count>             Number of newest results to show. Default: 100.");
        context.WriteLine("  --newest-files-only        Search only newest files.");
        context.WriteLine("  --newest-file-count <n>    Number of newest files when newest-only is enabled. Default: 10.");
        context.WriteLine("  --help                     Show this help.");
    }
}
