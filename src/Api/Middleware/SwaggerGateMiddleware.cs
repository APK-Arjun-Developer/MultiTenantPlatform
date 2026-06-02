using System.Security.Cryptography;
using System.Text;
using Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public sealed class SwaggerGateMiddleware
{
    private const string CookieName = "SwaggerGate";
    private const string LoginPath = "/swagger/login";
    private const string LogoutPath = "/swagger/logout";
    private const string ProtectorPurpose = "SwaggerGate.v1";

    private readonly RequestDelegate _next;
    private readonly IDataProtector _protector;
    private readonly SwaggerAccessOptions _options;

    public SwaggerGateMiddleware(
        RequestDelegate next,
        IDataProtectionProvider dataProtection,
        IOptions<SwaggerAccessOptions> options)
    {
        _next = next;
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (path.Equals(LogoutPath, StringComparison.OrdinalIgnoreCase))
        {
            await HandleLogoutAsync(context);
            return;
        }

        if (path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                await HandleLoginPostAsync(context);
                return;
            }

            await WriteLoginPageAsync(context, error: null);
            return;
        }

        if (!IsAuthenticated(context))
        {
            var returnUrl = Uri.EscapeDataString(path + context.Request.QueryString);
            context.Response.Redirect($"{LoginPath}?returnUrl={returnUrl}");
            return;
        }

        await _next(context);
    }

    private async Task HandleLoginPostAsync(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync();
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();

        if (!ValidateCredentials(username, password))
        {
            await WriteLoginPageAsync(context, "Invalid username or password.");
            return;
        }

        var expires = DateTimeOffset.UtcNow.AddHours(_options.SessionHours);
        var payload = $"{username}|{expires:O}";
        var protectedValue = _protector.Protect(payload);

        context.Response.Cookies.Append(
            CookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = expires,
                Path = "/swagger",
            });

        var redirect = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
            ? "/swagger"
            : returnUrl;

        context.Response.Redirect(redirect);
    }

    private Task HandleLogoutAsync(HttpContext context)
    {
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/swagger" });
        context.Response.Redirect(LoginPath);
        return Task.CompletedTask;
    }

    private bool IsAuthenticated(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var payload = _protector.Unprotect(value);
            var separator = payload.IndexOf('|', StringComparison.Ordinal);
            if (separator < 0)
            {
                return false;
            }

            var expiryRaw = payload[(separator + 1)..];
            if (!DateTimeOffset.TryParse(
                    expiryRaw,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var expires))
            {
                return false;
            }

            return expires > DateTimeOffset.UtcNow;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private bool ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            return false;
        }

        var expectedUser = _options.AdminUsername.Trim();
        var providedUser = username.Trim();

        return FixedTimeEquals(expectedUser, providedUser)
            && FixedTimeEquals(_options.AdminPassword, password);
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (expectedBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static async Task WriteLoginPageAsync(HttpContext context, string? error)
    {
        var returnUrl = context.Request.Query["returnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
        {
            returnUrl = "/swagger";
        }

        var errorHtml = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""<p class="error">{System.Net.WebUtility.HtmlEncode(error)}</p>""";

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = StatusCodes.Status200OK;

        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Swagger sign in</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #0f172a; color: #e2e8f0; margin: 0; min-height: 100vh; display: grid; place-items: center; }
                main { background: #1e293b; padding: 2rem; border-radius: 12px; width: min(400px, 92vw); box-shadow: 0 10px 40px rgba(0,0,0,.35); }
                h1 { margin: 0 0 .5rem; font-size: 1.35rem; }
                p { margin: 0 0 1rem; color: #94a3b8; font-size: .95rem; }
                label { display: block; margin-bottom: .35rem; font-size: .85rem; }
                input { width: 100%; box-sizing: border-box; padding: .6rem .75rem; margin-bottom: 1rem; border: 1px solid #334155; border-radius: 8px; background: #0f172a; color: #e2e8f0; }
                button { width: 100%; padding: .7rem; border: 0; border-radius: 8px; background: #3b82f6; color: #fff; font-weight: 600; cursor: pointer; }
                button:hover { background: #2563eb; }
                .error { color: #fca5a5; margin-bottom: 1rem; }
              </style>
            </head>
            <body>
              <main>
                <h1>API documentation</h1>
                <p>Sign in with your system administrator account to open Swagger.</p>
                {{errorHtml}}
                <form method="post" action="/swagger/login">
                  <input type="hidden" name="returnUrl" value="{{System.Net.WebUtility.HtmlEncode(returnUrl)}}" />
                  <label for="username">Username</label>
                  <input id="username" name="username" type="text" autocomplete="username" required />
                  <label for="password">Password</label>
                  <input id="password" name="password" type="password" autocomplete="current-password" required />
                  <button type="submit">Sign in</button>
                </form>
              </main>
            </body>
            </html>
            """;

        await context.Response.WriteAsync(html);
    }
}
