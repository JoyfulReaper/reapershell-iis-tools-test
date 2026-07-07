# IIS Tools for ReaperShell

IIS Tools for ReaperShell is a small ReaperShell command pack for inspecting application/stdout logs and IIS W3C logs. It is aimed at developers and administrators who want a practical way to search for error signals, narrow results by HTTP status, and quickly prove which build of the command pack a shell session actually loaded.

## Quick Start

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj
```

Then load the pack in ReaperShell:

```text
repo add iis-tools C:\GitHub\reapershell-iis-tools-test
repo trust iis-tools
repo build iis-tools
repo load iis-tools
iis-error-search --help
```

## What It Does

This repository is a ReaperShell command pack. It currently provides two commands:

- `iis-error-search`
- `iis-tools-version`

`iis-error-search` searches:

- app/stdout logs for error-like text patterns
- IIS W3C logs for configured HTTP status codes

It also supports IIS text filters for cases where you want to search the contents of IIS rows rather than only error codes.

`iis-tools-version` prints build metadata embedded when the DLL was built. `iis-error-search --version` prints the same information.

## Requirements

- ReaperShell installed or built locally
- .NET SDK 10.0 or later, because this project targets `net10.0`
- Windows if you want to use the default IIS log locations
- Read access to the log files you want to inspect

The default IIS path assumes a typical local IIS installation:

```text
C:\inetpub\logs\LogFiles\W3SVC*\*.log
```

## Installation / Loading

This repository is laid out as a ReaperShell command pack repo. The pack project lives at:

```text
commands/iis-error-search/IisErrorSearchCommand.csproj
```

The root `shellpack.json` points ReaperShell at the `commands` folder.

### Build from this checkout

If you have this repository checked out alongside `ReaperShell`, the project can usually resolve `ReaperShell.Abstractions` on its own:

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj
```

If your checkout layout is different, pass the abstraction project path explicitly:

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj /p:ReaperShellAbstractionsProject="C:\GitHub\ReaperShell\src\ReaperShell.Abstractions\ReaperShell.Abstractions.csproj"
```

### Load into ReaperShell

Register the repo, trust it, build it, and load it:

```text
repo add iis-tools C:\GitHub\reapershell-iis-tools-test
repo trust iis-tools
repo build iis-tools
repo load iis-tools
```

If the repo is already registered and trusted, you only need `repo build` and `repo load`.

## Commands

### `iis-error-search`

Search app/stdout logs for error-like text and IIS W3C logs for matching HTTP status codes and row content.

#### Syntax

```text
iis-error-search [options]
```

#### Defaults

- App log globs:
  - `.\logs\*.log`
  - `.\logs\stdout*.log`
- IIS log glob:
  - `C:\inetpub\logs\LogFiles\W3SVC*\*.log`
- Default app/stdout patterns:
  - `Exception`
  - `Unhandled`
  - `fail`
  - `failed`
  - `error`
  - `critical`
  - `StackTrace`
  - `NullReferenceException`
  - `InvalidOperationException`
  - `ArgumentException`
- Default IIS status codes:
  - `400`
  - `401`
  - `403`
  - `404`
  - `410`
  - `429`
  - `500`
  - `502`
  - `503`
- Default result limit:
  - `100`
- Default newest-file count when `--newest-files-only` is enabled:
  - `10`

#### Options

| Option | Meaning | Notes |
| --- | --- | --- |
| `--app-log <glob>` | Select app/stdout log files to search. | Can be repeated. Alias: `--app-log-path`. The first explicit use replaces the default app globs; later repeats append. |
| `--iis-log <glob>` | Select IIS W3C log files to search. | Can be repeated. Alias: `--iis-log-path`. This selects files only and does not search text inside IIS logs. The first explicit use replaces the default IIS glob; later repeats append. |
| `--pattern <text>` | Search app/stdout log lines for text. | Can be repeated. Alias: `--app-pattern`. The first explicit use replaces the default patterns; later repeats append. |
| `--status <code[,code]>` | Filter IIS rows by HTTP status code. | Can be repeated. Alias: `--iis-status`. Comma-separated values are accepted. Valid range is `100` to `599`. The first explicit use replaces the default IIS status list; later repeats append. |
| `--all-statuses` | Search IIS rows across all statuses. | Useful with IIS text filters when you do not want status filtering. |
| `--iis-contains <text>` | Search useful IIS row fields for text. | Can be repeated. Case-insensitive. |
| `--user-agent <text>` | Search `cs(User-Agent)` for text. | Can be repeated. Case-insensitive. Alias: `--ua`. |
| `--url <text>` | Search the combined IIS URL, including query string when present. | Can be repeated. Case-insensitive. |
| `--last <count>` | Limit the number of newest results shown. | Default: `100`. |
| `--newest-files-only` | Search only the newest files. | Uses `--newest-file-count` to decide how many files to keep. |
| `--newest-file-count <n>` | Number of newest files to search when newest-only mode is enabled. | Default: `10`. |
| `--since <value>` | Only show results at or after a cutoff time. | Accepts relative values like `30m`, `2h`, `1d`, or a local/ISO datetime string such as `2026-07-05T14:30:00`. |
| `--verbose` | Print warnings for skipped paths and unreadable files. | Warnings are written to the error stream. |
| `--fail-on-match` | Return exit code `2` if any app or IIS matches are found. | Otherwise the command returns `0` for a successful run and `1` for invalid arguments. |
| `--version` | Show loaded command pack version/build info. | Prints the same metadata as `iis-tools-version`. |
| `--help` | Show usage help. | Aliases: `-h`, `/?`. |

#### Behavior Notes

- If you pass IIS text filters such as `--iis-contains`, `--user-agent`, or `--url` without `--status`, the command searches all IIS statuses by default.
- If you pass `--status`, it remains an explicit IIS status filter.
- If you pass both text filters and `--all-statuses`, IIS status filtering is disabled entirely.
- Relative paths are resolved from the shell working directory.
- Repeated `--app-log`, `--iis-log`, `--pattern`, and `--status` options append after the first explicit override.

### `iis-tools-version`

Print build metadata for the loaded command pack.

#### Syntax

```text
iis-tools-version
```

#### What It Prints

This command prints metadata embedded when the assembly was built, including:

- command pack name
- assembly name
- assembly version
- assembly informational version
- build configuration
- Git branch
- Git commit SHA
- short Git commit SHA
- dirty working tree flag
- build timestamp in UTC
- loaded assembly path

`iis-error-search --version` prints the same output.

## Common Examples

```text
iis-error-search
iis-error-search --status 404
iis-error-search --status 404,500 --last 25
iis-error-search --iis-log "C:\inetpub\logs\LogFiles\W3SVC*\*.log"
iis-error-search --app-log ".\logs\*.log" --pattern Exception
iis-error-search --newest-files-only --newest-file-count 3
iis-error-search --version
iis-tools-version
```

Additional useful examples:

```text
iis-error-search --user-agent bot
iis-error-search --iis-contains bot --all-statuses
iis-error-search --iis-log "C:\inetpub\logs\LogFiles\W3SVC*\*.log" --user-agent bot
iis-error-search --since 2h --verbose
iis-error-search --fail-on-match
```

## Understanding IIS W3C Logs

IIS W3C logs are the standard text-based access logs produced by IIS. They are usually stored under:

```text
C:\inetpub\logs\LogFiles\W3SVC*\*.log
```

`iis-error-search` parses IIS rows using the `#Fields:` header in each log file. That header defines the column order for the rows that follow. If a file does not contain a `#Fields:` line, the command skips rows until it finds one.

The command reads common fields such as:

- `date`
- `time`
- `cs-method`
- `cs-uri-stem`
- `cs-uri-query`
- `cs(User-Agent)`
- `cs(Referer)`
- `sc-status`
- `sc-substatus`
- `sc-win32-status`
- `time-taken`

## Output

The command prints separate sections for app/stdout matches and IIS log matches.

### App/stdout matches

Each app/stdout match includes:

- the source file
- the line number
- the line content
- the file modification time, or a parsed timestamp when the line includes one

App/stdout search is best-effort timestamp aware. When a line does not contain a parseable timestamp, the command falls back to the file’s `LastWriteTimeUtc` for ordering and time filtering.

### IIS matches

Each IIS match includes:

- HTTP status code
- method
- URL
- UTC date and time
- status/substatus
- Win32 status
- request time-taken
- source file and line number
- referer when present
- user agent when present

### Summary

The summary groups IIS matches by HTTP status and then lists the most frequent URLs.

### Status fields

- `sc-status` is the main HTTP status code, such as `200`, `404`, or `500`
- `sc-substatus` is the IIS-specific substatus code
- `sc-win32-status` is the Windows/Win32 result code IIS recorded for the request

## Version Diagnostics

`iis-tools-version` exists so you can prove which branch, commit, and DLL ReaperShell actually loaded. That matters because a runtime branch check can be misleading if the checkout changed after the DLL was built.

The version output is based on build-time metadata embedded into the assembly. It is not reconstructed from the live checkout at command time.

Use it when you want to confirm:

- which branch built the DLL
- which commit was embedded at build time
- whether the loaded DLL came from a dirty working tree
- which assembly path ReaperShell loaded

## Troubleshooting

- `No IIS log files found`
  - `--iis-log` selects files by glob, not text.
  - Confirm the path exists and that the glob matches real `.log` files.
  - On a local IIS machine, start with the default path: `C:\inetpub\logs\LogFiles\W3SVC*\*.log`.
  - Try `--verbose` to see skipped paths and unreadable files.

- `Access denied`
  - Make sure your account can read the log directory.
  - Run ReaperShell under an account with access to the log files.
  - If the logs are in a protected location, copy them somewhere readable and point `--iis-log` at the copy.

- `Unknown option`
  - Run `iis-error-search --help` and check the exact option name.
  - Remember that `--iis-log` and `--app-log` are file globs, not search terms.
  - Aliases exist for some options, such as `--ua` and `--app-log-path`.

- `I passed --iis-log *bot* and it did not search for bots`
  - That is expected.
  - `--iis-log` chooses which log files to read.
  - Use `--user-agent bot` or `--iis-contains bot` to search IIS log content.

- `ReaperShell is loading the wrong branch`
  - Run `iis-tools-version` and compare the embedded commit and branch with the checkout you expect.
  - Rebuild the command pack, then reload it in ReaperShell.
  - If the version output still looks stale, the host may be loading a cached or older DLL from a different repo path.

- `Reload did not pick up my new command`
  - Rebuild the pack with `repo build <name>`.
  - Reload it with `repo load <name>` or `repo reload <name>` if your shell session supports that command.
  - Confirm the command pack repo you loaded is the one you actually edited.

- `Version command shows unexpected branch/commit`
  - The metadata is embedded at build time.
  - Rebuild the DLL from the checkout you want to report.
  - Check `git status` and `git rev-parse HEAD` in the repo that produced the binary.

## Development

Build the command pack project with:

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj
```

If your checkout layout does not let MSBuild locate `ReaperShell.Abstractions` automatically, pass the project path explicitly:

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj /p:ReaperShellAbstractionsProject="C:\GitHub\ReaperShell\src\ReaperShell.Abstractions\ReaperShell.Abstractions.csproj"
```

Manual smoke tests in ReaperShell:

```text
iis-tools-version
iis-error-search --version
iis-error-search --help
iis-error-search --status 404,500 --last 10
iis-error-search --user-agent bot
iis-error-search --iis-contains bot --all-statuses
```

Generated build artifacts live under `bin/` and `obj/`. Keep those directories out of source control.

## Limitations

- This is not a full log analytics engine.
- There is no indexing layer.
- Large log files can take time to scan.
- IIS parsing depends on valid W3C log format and the presence of a `#Fields:` header.
- Read permissions still matter.

## License

No explicit license file is included in this repository.
