namespace Infrastructure.Email;

/// <summary>
/// Responsive HTML email templates.
/// Each method returns a complete HTML document ready to send as the email body.
/// The shared layout uses a single-column 600px centre-column that renders
/// correctly in Gmail, Outlook, Apple Mail, and mobile clients.
/// </summary>
internal static class EmailTemplates
{
    // ── Shared layout ─────────────────────────────────────────────────────────

    private static string Layout(string title, string preheader, string bodyContent) => $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
  <title>{Encode(title)}</title>
  <!--[if mso]><noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript><![endif]-->
  <style>
    body {{ margin:0; padding:0; background:#f4f6f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }}
    .wrapper {{ width:100%; background:#f4f6f9; padding:40px 0; }}
    .container {{ max-width:600px; margin:0 auto; background:#ffffff; border-radius:8px; overflow:hidden; box-shadow:0 2px 8px rgba(0,0,0,.08); }}
    .header {{ background:#1a56db; padding:32px 40px; text-align:center; }}
    .header h1 {{ margin:0; color:#ffffff; font-size:22px; font-weight:700; letter-spacing:-.3px; }}
    .body {{ padding:40px; color:#374151; font-size:15px; line-height:1.6; }}
    .body h2 {{ margin:0 0 16px; font-size:20px; color:#111827; font-weight:600; }}
    .body p {{ margin:0 0 16px; }}
    .btn-wrap {{ text-align:center; margin:32px 0; }}
    .btn {{ display:inline-block; background:#1a56db; color:#ffffff !important; text-decoration:none; font-weight:600; font-size:15px; padding:14px 32px; border-radius:6px; letter-spacing:.3px; }}
    .btn:hover {{ background:#1648c2; }}
    .note {{ background:#f9fafb; border:1px solid #e5e7eb; border-radius:6px; padding:16px; font-size:13px; color:#6b7280; margin-top:8px; }}
    .divider {{ height:1px; background:#e5e7eb; margin:24px 0; }}
    .footer {{ background:#f9fafb; border-top:1px solid #e5e7eb; padding:24px 40px; text-align:center; font-size:12px; color:#9ca3af; line-height:1.5; }}
    .footer a {{ color:#6b7280; text-decoration:none; }}
    @media only screen and (max-width:620px) {{
      .container {{ border-radius:0 !important; }}
      .body {{ padding:24px !important; }}
      .header {{ padding:24px !important; }}
      .footer {{ padding:16px 24px !important; }}
    }}
  </style>
</head>
<body>
  <!-- Preheader (hidden preview text) -->
  <span style=""display:none;max-height:0;overflow:hidden;"">{Encode(preheader)}&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;</span>
  <div class=""wrapper"">
    <table class=""container"" cellpadding=""0"" cellspacing=""0"" border=""0"" role=""presentation"" width=""600"" align=""center"">
      <tr><td class=""header""><h1>MultiTenant Platform</h1></td></tr>
      <tr><td class=""body"">{bodyContent}</td></tr>
      <tr><td class=""footer"">
        &copy; {DateTime.UtcNow.Year} MultiTenant Platform. All rights reserved.<br />
        This email was sent to you because an account action was performed on your behalf.<br />
        If you did not request this, please ignore this email or <a href=""#"">contact support</a>.
      </td></tr>
    </table>
  </div>
</body>
</html>";

    // ── Templates ─────────────────────────────────────────────────────────────

    internal static string AccountSetup(string fullName, string setupUrl) =>
        Layout(
            title: "Set up your account",
            preheader: $"Hi {fullName}, your account is ready — click below to set your password.",
            bodyContent: $@"
<h2>Set up your account</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>An account has been created for you on MultiTenant Platform. Click the button below to set your password and activate your account.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{setupUrl}"">Set up my account</a></div>
<div class=""note"">
  <strong>This link expires in 7 days.</strong><br />
  If the button above doesn't work, copy and paste this URL into your browser:<br />
  <a href=""{setupUrl}"" style=""color:#1a56db;word-break:break-all;"">{setupUrl}</a>
</div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">If you weren't expecting this email, you can safely ignore it. No account will be activated without completing the setup.</p>");

    internal static string Welcome(string fullName, string loginUrl) =>
        Layout(
            title: "Welcome to MultiTenant Platform",
            preheader: $"Welcome aboard, {fullName}! Your account is now active.",
            bodyContent: $@"
<h2>Welcome aboard! 🎉</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>Your account is now active and ready to use. Click the button below to log in and get started.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{loginUrl}"">Go to my account</a></div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">If you have any questions, reach out to your administrator or our support team.</p>");

    internal static string TenantAdminInvitation(string toEmail, string invitationUrl, string tenantName) =>
        Layout(
            title: $"You're invited to manage {tenantName}",
            preheader: $"You've been invited as an administrator of {tenantName}.",
            bodyContent: $@"
<h2>You've been invited</h2>
<p>Hi there,</p>
<p>You've been invited to join <strong>{Encode(tenantName)}</strong> as an administrator on MultiTenant Platform.</p>
<p>Click the button below to accept your invitation and create your account.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{invitationUrl}"">Accept invitation</a></div>
<div class=""note"">
  <strong>This invitation expires in 7 days.</strong><br />
  If the button above doesn't work, copy and paste this URL into your browser:<br />
  <a href=""{invitationUrl}"" style=""color:#1a56db;word-break:break-all;"">{invitationUrl}</a>
</div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">This invitation was sent to <strong>{Encode(toEmail)}</strong>. If you weren't expecting it, you can safely ignore this email.</p>");

    internal static string TenantUserInvitation(string toEmail, string invitationUrl, string tenantName) =>
        Layout(
            title: $"You're invited to join {tenantName}",
            preheader: $"You've been invited to join {tenantName}.",
            bodyContent: $@"
<h2>You're invited to join {Encode(tenantName)}</h2>
<p>Hi there,</p>
<p>You've been invited to join <strong>{Encode(tenantName)}</strong> on MultiTenant Platform. Click the button below to accept your invitation and create your account.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{invitationUrl}"">Accept invitation</a></div>
<div class=""note"">
  <strong>This invitation expires in 7 days.</strong><br />
  If the button above doesn't work, copy and paste this URL into your browser:<br />
  <a href=""{invitationUrl}"" style=""color:#1a56db;word-break:break-all;"">{invitationUrl}</a>
</div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">This invitation was sent to <strong>{Encode(toEmail)}</strong>. If you weren't expecting it, you can safely ignore this email.</p>");

    internal static string AccountActivation(string fullName, string loginUrl) =>
        Layout(
            title: "Your account has been activated",
            preheader: $"Hi {fullName}, your account has been reactivated.",
            bodyContent: $@"
<h2>Account activated</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>Your account on MultiTenant Platform has been activated. You can now log in using your existing credentials.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{loginUrl}"">Log in</a></div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">If you did not expect this, please contact your administrator immediately.</p>");

    internal static string AccountDeactivation(string fullName) =>
        Layout(
            title: "Your account has been deactivated",
            preheader: $"Hi {fullName}, your account access has been suspended.",
            bodyContent: $@"
<h2>Account deactivated</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>Your account on MultiTenant Platform has been deactivated by an administrator. You will not be able to log in until your account is reactivated.</p>
<p>If you believe this was done in error, please contact your administrator.</p>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">If you did not expect this, please contact your administrator immediately.</p>");

    internal static string EmailVerificationOtp(string fullName, string otp) =>
        Layout(
            title: "Verify your email address",
            preheader: $"Hi {fullName}, here is your email verification code.",
            bodyContent: $@"
<h2>Verify your email address</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>Use the verification code below to confirm your email address. Enter it in the app to complete verification.</p>
<div class=""btn-wrap"">
  <div style=""display:inline-block;background:#f3f4f6;border:2px dashed #1a56db;border-radius:8px;padding:16px 40px;"">
    <span style=""font-size:36px;font-weight:700;letter-spacing:10px;color:#1a56db;font-family:monospace;"">{Encode(otp)}</span>
  </div>
</div>
<div class=""note"">
  <strong>This code expires in 15 minutes.</strong><br />
  If you did not request this code, you can safely ignore this email.
</div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">Never share this code with anyone. Our team will never ask for it.</p>");

    internal static string PasswordReset(string fullName, string resetUrl) =>
        Layout(
            title: "Reset your password",
            preheader: "We received a request to reset your password. Click below to continue.",
            bodyContent: $@"
<h2>Reset your password</h2>
<p>Hi <strong>{Encode(fullName)}</strong>,</p>
<p>We received a request to reset the password for your account. Click the button below to choose a new password.</p>
<div class=""btn-wrap""><a class=""btn"" href=""{resetUrl}"">Reset my password</a></div>
<div class=""note"">
  <strong>This link expires in 24 hours.</strong><br />
  If the button above doesn't work, copy and paste this URL into your browser:<br />
  <a href=""{resetUrl}"" style=""color:#1a56db;word-break:break-all;"">{resetUrl}</a>
</div>
<div class=""divider""></div>
<p style=""font-size:13px;color:#6b7280;"">If you did not request a password reset, you can safely ignore this email. Your password will not be changed.</p>");

    // ── Safety ────────────────────────────────────────────────────────────────

    private static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
