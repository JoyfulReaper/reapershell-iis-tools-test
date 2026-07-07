using System.Reflection;
using ReaperShell.Abstractions;

namespace IisErrorSearchCommand;

public static class VersionInfoWriter
{
    public static void Write(ShellContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var assemblyPath = string.IsNullOrWhiteSpace(assembly.Location) ? "unknown" : assembly.Location;
        var version = assemblyName.Version?.ToString() ?? "unknown";
        var commandPackName = BuildInfo.CommandPackName;

        context.WriteLine(commandPackName);
        context.WriteLine($"Assembly: {assemblyName.Name ?? "unknown"}");
        context.WriteLine($"Version: {version}");
        context.WriteLine($"Informational Version: {informationalVersion}");
        context.WriteLine($"Build Configuration: {BuildInfo.BuildConfiguration}");
        context.WriteLine($"Git Branch: {BuildInfo.GitBranch}");
        context.WriteLine($"Git Commit: {BuildInfo.GitCommit}");
        context.WriteLine($"Git Commit Short: {BuildInfo.GitCommitShort}");
        context.WriteLine($"Git Dirty: {BuildInfo.GitDirty}");
        context.WriteLine($"Build UTC: {BuildInfo.BuildUtc}");
        context.WriteLine($"Assembly Path: {assemblyPath}");
    }

    public static void WriteUsage(ShellContext context)
    {
        context.WriteLine("Usage:");
        context.WriteLine("  iis-tools-version [--help]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  --help   Show this help. Aliases: -h, /?.");
    }
}
