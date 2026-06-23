using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Infrastructure.Persistence;
using Infrastructure.Identity;
using Api.Contracts;
using Api.Extensions;
using Api.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, config) =>
{
    config.Enrich.FromLogContext();
    config.WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} tenant={tenant_id} user={user_id} corr={correlation_id}{NewLine}{Exception}");
    config.WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] tenant={tenant_id} user={user_id} corr={correlation_id} {Message:lj}{NewLine}{Exception}");
});

// ── AppBaseUrl guard ─────────────────────────────────────────────────────────
// AppBaseUrl drives every emailed link. In non-dev environments it must be an
// HTTPS URL — never the placeholder "https://app.example.com".
if (!builder.Environment.IsDevelopment())
{
    var appBaseUrl = builder.Configuration["AppBaseUrl"] ?? string.Empty;

    if (string.IsNullOrWhiteSpace(appBaseUrl) ||
        appBaseUrl.Equals("https://app.example.com", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "AppBaseUrl must be configured to a real HTTPS URL in non-Development environments. " +
            "Set it via environment variable AppBaseUrl or a secrets manager.");
    }

    if (!appBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"AppBaseUrl must start with 'https://' — got '{appBaseUrl}'. " +
            "Email tokens must only be transmitted over TLS.");
    }
}

// ── Feature flags ────────────────────────────────────────────────────────────
builder.Services.Configure<Application.Options.FeatureOptions>(
    builder.Configuration.GetSection(Application.Options.FeatureOptions.SectionName));

// ── Persistence & Identity ───────────────────────────────────────────────────
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddEmailInfrastructure(builder.Configuration, builder.Environment);

// ── RBAC Hierarchy Policies ───────────────────────────────────────────────────
// These are checked by [Authorize(Policy="...")] as an alternative to permission checks
// where the requirement is purely about caller scope (not a specific permission).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("SystemAdminOnly", policy =>
        policy.RequireClaim("tenant_id", Guid.Empty.ToString()))
    .AddPolicy("TenantAdminOrAbove", policy =>
        policy.RequireAssertion(ctx =>
        {
            var claim = ctx.User.FindFirst("system_role")?.Value;
            // SystemAdmin=1, TenantAdmin=2 — value <= 2 means TenantAdmin or above.
            return int.TryParse(claim, out var v) && v <= 2;
        }))
    .AddPolicy("AuthenticatedTenantUser", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  ctx.User.FindFirst("tenant_id")?.Value is string tid &&
                  tid != Guid.Empty.ToString()));

// ── Response Compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ── Controllers & Validation ──────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var envelope = ApiEnvelopeFactory.ValidationError(
                ApiEnvelopeFactory.FromModelState(context.ModelState),
                context.HttpContext.TraceIdentifier);

            return new BadRequestObjectResult(envelope);
        };
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Application.Validators.OnboardTenantRequestValidator>();

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", policy =>
    {
        policy.PermitLimit = 10;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueLimit = 0;
        policy.AutoReplenishment = true;
    });
});

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Infrastructure.Persistence.Contexts.ApplicationDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy)
    .Add(new HealthCheckRegistration(
        "email",
        sp => sp.GetRequiredService<Infrastructure.Email.SmtpHealthCheck>(),
        failureStatus: HealthStatus.Degraded,
        tags: ["email"]));

// ── OpenAPI / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Multi-Tenant Platform API",
        Version = "v1",
        Description =
            "JWT multi-tenant API. See docs/API.md for login rules, onboarding, list scoping, and pagination.",
    });

    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(bearerScheme, new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference(bearerScheme, document)] = [],
    });
});

// ── Forwarded Headers (for correct client IP behind proxy) ────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
// Order matters. Keep this order.

app.UseForwardedHeaders();

app.UseHttpsRedirection();

// Security headers on every response
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseResponseCompression();

app.UseCors("Default");

app.UseRateLimiter();

if (app.IsSwaggerEnabled(app.Configuration))
{
    app.UseSwaggerDocumentation();
}

app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            }),
        });
        await context.Response.WriteAsync(result);
    }
});

await DatabaseInitializer.ApplyMigrationsAndSeedAsync(
    app.Services,
    app.Configuration,
    app.Logger);

app.Run();
