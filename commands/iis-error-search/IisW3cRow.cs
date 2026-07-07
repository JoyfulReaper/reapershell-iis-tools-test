using System;
using System.Collections.Generic;

namespace IisErrorSearchCommand;

public sealed record IisW3cRow(
    DateTimeOffset? TimestampUtc,
    IReadOnlyDictionary<string, string> Fields);
