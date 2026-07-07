using System;

namespace IisErrorSearchCommand;

public sealed record AppMatch(
    DateTime LastWriteTimeUtc,
    string File,
    int LineNumber,
    string Line);
