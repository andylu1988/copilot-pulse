using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace CopilotPulse;

internal static class WindowsSso
{
    private const string CopilotClientId = "2efaf292-00a0-426c-ba7d-f5d2b214b8fc";
    private static readonly string? Principal = ReadWindowsPrincipal();
    internal static bool HasWindowsPrincipal => Principal != null;

    [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool GetUserNameEx(int nameFormat, StringBuilder name, ref uint size);

    private static string? ReadWindowsPrincipal()
    {
        var buffer = new StringBuilder(512);
        uint length = 512;
        if (!GetUserNameEx(8 /* NameUserPrincipal */, buffer, ref length)) return null;
        string value = buffer.ToString().Trim();
        return value.Length <= 254 && value.Contains('@') && !value.Contains(' ') ? value : null;
    }

    // A username hint is not a token. Leave every app-owned OAuth parameter intact.
    // Restrict it to the exact Copilot app registration and callback already used by this website.
    internal static string? HintForCopilotAuthorization(string url, string? principal)
    {
        if (string.IsNullOrWhiteSpace(principal) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != "https" || uri.Host != "login.microsoftonline.com" ||
            !(uri.AbsolutePath.EndsWith("/oauth2/v2.0/authorize", StringComparison.Ordinal) || uri.AbsolutePath.EndsWith("/oauth2/authorize", StringComparison.Ordinal))) return null;
        var query = HttpUtility.ParseQueryString(uri.Query);
        if (query["client_id"] != CopilotClientId || !string.IsNullOrEmpty(query["login_hint"]) || !string.IsNullOrEmpty(query["prompt"])) return null;
        if (!Uri.TryCreate(query["redirect_uri"], UriKind.Absolute, out var callback) || callback.Scheme != "https" || callback.Host != "copilot.github.microsoft.com" || callback.AbsolutePath != "/auth/entra-id/callback") return null;
        var builder = new UriBuilder(uri) { Query = uri.Query.TrimStart('?') + "&login_hint=" + Uri.EscapeDataString(principal) };
        return builder.Uri.AbsoluteUri;
    }

    internal static string? HintForCopilotAuthorization(string url) => HintForCopilotAuthorization(url, Principal);
}
