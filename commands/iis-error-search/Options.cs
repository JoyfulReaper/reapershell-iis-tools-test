using System.Collections.Generic;

namespace IisErrorSearchCommand;

public sealed class Options
{
    public List<string> AppLogPaths { get; } =
    [
        @".\logs\*.log",
        @".\logs\stdout*.log"
    ];

    public List<string> IisLogPaths { get; } =
    [
        @"C:\inetpub\logs\LogFiles\W3SVC*\*.log"
    ];

    public List<string> AppPatterns { get; } =
    [
        "Exception",
        "Unhandled",
        "fail",
        "failed",
        "error",
        "critical",
        "StackTrace",
        "NullReferenceException",
        "InvalidOperationException",
        "ArgumentException"
    ];

    public HashSet<int> IisStatusCodes { get; } =
    [
        400,
        401,
        403,
        404,
        410,
        429,
        500,
        502,
        503
    ];

    public int Last { get; set; } = 100;

    public bool NewestFilesOnly { get; set; }

    public int NewestFileCount { get; set; } = 10;

    public bool ShowHelp { get; set; }
}
