using System.Collections.Generic;

namespace IisErrorSearchCommand;

public static class IisStatusHintProvider
{
    public static IReadOnlyList<string> GetHints(IisMatch match)
    {
        var hints = new List<string>();

        if (match.Status == 500 && match.SubStatus == "19")
        {
            hints.Add("500.19: IIS configuration error. Check web.config and file permissions.");
        }
        else
        {
            switch (match.Status)
            {
                case 401:
                    hints.Add("401: Unauthorized. Check authentication configuration.");
                    break;
                case 403:
                    hints.Add("403: Forbidden. Check authorization, directory browsing, or request filtering.");
                    break;
                case 404:
                    hints.Add("404: Not found. Often scanner noise, stale links, or missing static files.");
                    break;
                case 500:
                    hints.Add("500: Server error. Check application logs around the same timestamp.");
                    break;
                case 502:
                    hints.Add("502: Bad gateway. Check upstream process, reverse proxy, or app pool health.");
                    break;
                case 503:
                    hints.Add("503: Service unavailable. Check app pool state and rapid-fail protection.");
                    break;
            }
        }

        switch (match.Win32Status)
        {
            case "5":
                hints.Add("Win32=5 means access denied. Check permissions.");
                break;
            case "32":
                hints.Add("Win32=32 often means the file is in use by another process.");
                break;
            case "64":
                hints.Add("Win32=64 often means the client disconnected.");
                break;
        }

        return hints;
    }
}
