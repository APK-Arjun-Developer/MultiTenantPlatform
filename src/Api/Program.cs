using Serilog;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Persistence.Seed;
using Infrastructure.Identity;
using Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Identity;
using Infrastructure.Identity.Entities;
using Microsoft.OpenApi;
using Api.Contracts;
using Api.Middleware;
using Application.Interfaces.Caching;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config.Enrich.FromLogContext();
    config.WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} tenant={tenant_id} user={user_id}{NewLine}{Exception}");
    config.WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] tenant={tenant_id} user={user_id} {Message:lj}{NewLine}{Exception}");
});

builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddControllers()
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

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Multi-Tenant Platform API",
        Version = "v1",
        Description =
            "JWT multi-tenant API. See docs/API.md for login rules, onboarding, list scoping, and pagination.",
    });

    const string bearerScheme = "Bearer";

    options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerScheme, document)] = [],
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseStaticFiles();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.EnableTryItOutByDefault();
        options.EnablePersistAuthorization();
        options.InjectJavascript("/swagger-ui/swagger-auto-auth.js");
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();

app.UseMiddleware<TenantMiddleware>();

app.UseAuthorization();

app.UseMiddleware<RequestLoggingMiddleware>();

app.MapControllers();

if (app.Configuration.GetValue("SeedOnStartup", app.Environment.IsDevelopment()))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    await DbSeeder.SeedAsync(dbContext);

    await PermissionSeeder.SeedAsync(dbContext);

    services.GetRequiredService<IAppCache>().InvalidatePermissionCatalogs();

    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    await IdentitySeeder.SeedAsync(roleManager, userManager, dbContext);
}

app.Run();