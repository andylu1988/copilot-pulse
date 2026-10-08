using CopilotPulse;
using System.Web;

const string root = "https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize?client_id=2efaf292-00a0-426c-ba7d-f5d2b214b8fc&redirect_uri=https%3A%2F%2Fcopilot.github.microsoft.com%2Fauth%2Fentra-id%2Fcallback&state=state%2Bvalue&nonce=nonce-123";
int passed = 0;
void Check(string name, bool condition) { if (!condition) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
string? hint = WindowsSso.HintForCopilotAuthorization(root, "example@example.test");
Check("standard hint added for the intended application", hint != null && HttpUtility.ParseQueryString(new Uri(hint).Query)["login_hint"] == "example@example.test");
Check("OAuth state and nonce preserved", hint!.Contains("state=state%2Bvalue&nonce=nonce-123"));
Check("existing account hint preserved", WindowsSso.HintForCopilotAuthorization(root + "&login_hint=chosen", "a@b.test") == null);
Check("explicit server prompt respected", WindowsSso.HintForCopilotAuthorization(root + "&prompt=select_account", "a@b.test") == null);
Check("other app registrations excluded", WindowsSso.HintForCopilotAuthorization(root.Replace("2efaf292-00a0-426c-ba7d-f5d2b214b8fc", "other-app"), "a@b.test") == null);
Check("unrelated callback excluded", WindowsSso.HintForCopilotAuthorization(root.Replace("copilot.github.microsoft.com", "example.org"), "a@b.test") == null);
Check("other login hosts excluded", WindowsSso.HintForCopilotAuthorization(root.Replace("login.microsoftonline.com", "example.org"), "a@b.test") == null);
Check("non-HTTPS excluded", WindowsSso.HintForCopilotAuthorization(root.Replace("https:", "http:"), "a@b.test") == null);
Check("missing Windows identity leaves website unchanged", WindowsSso.HintForCopilotAuthorization(root, null) == null);
Console.WriteLine($"{passed} SSO routing checks passed.");
