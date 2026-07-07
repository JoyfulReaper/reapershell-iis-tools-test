using System;

namespace IisErrorSearchCommand;

public sealed record LogFile(
    string FullName,
    DateTime LastWriteTimeUtc);
