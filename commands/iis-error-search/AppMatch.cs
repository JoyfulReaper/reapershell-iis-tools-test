using System;

namespace IisErrorSearchCommand;

public sealed record AppMatch(
    DateTimeOffset SortTimeUtc,
    DateTime LastWriteTimeUtc,
    bool HasParsedTimestamp,
    string File,
    int LineNumber,
    string Line);
