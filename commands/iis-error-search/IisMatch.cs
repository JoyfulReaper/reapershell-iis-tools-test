using System;

namespace IisErrorSearchCommand;

public sealed record IisMatch(
    string File,
    DateTime LastWriteTimeUtc,
    int LineNumber,
    string Date,
    string Time,
    string Method,
    string Url,
    int Status,
    string SubStatus,
    string Win32Status,
    string TimeTakenMs,
    string Referer,
    string UserAgent);
