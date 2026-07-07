Example command pack for ReaperShell.

## Build

This pack expects a sibling checkout of `ReaperShell` so the `ReaperShell.Abstractions` project reference can resolve.

```powershell
dotnet build commands\iis-error-search\IisErrorSearchCommand.csproj
```

## Usage

```text
iis-error-search --help
iis-error-search --app-log ".\logs\*.log" --iis-log "C:\inetpub\logs\LogFiles\W3SVC*\*.log"
iis-error-search --pattern "Exception" --status 404,500 --last 25
iis-error-search --since 2h --verbose
iis-error-search --fail-on-match
iis-error-search --user-agent bot
iis-error-search --iis-contains bot --all-statuses
iis-error-search --iis-log "C:\inetpub\logs\LogFiles\W3SVC*\*.log" --user-agent bot
```

## Smoke Test

If you have ReaperShell loading this command pack, a quick manual check is:

```text
iis-error-search --help
iis-error-search --status 404,500
iis-error-search --since 30m
iis-error-search --user-agent bot
```

## Notes

- `--app-log` and `--iis-log` are file glob patterns, not search terms.
- `--iis-contains`, `--user-agent`/`--ua`, and `--url` search IIS rows.
- Relative paths resolve from the shell working directory.
- Repeated `--app-log`, `--iis-log`, `--pattern`, and `--status` options append after the first explicit override.
- `--since` accepts relative durations like `30m`, `2h`, `1d`, or a local/ISO datetime string.
- `--verbose` prints warnings for skipped paths and unreadable files.
- `--fail-on-match` returns exit code `2` when any matches are found.
- When you use IIS text filters without `--status`, the command searches all IIS statuses by default.
