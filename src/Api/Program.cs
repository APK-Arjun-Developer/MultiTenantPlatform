using Serilog;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Persistence.Seed;
using Infrastructure.Identity;
using Infrastructure.Identity.Seed;
using Microsoft.AspNetCore.Identity;
using Infrastructure.Identity.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console();
    config.WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day);
});

builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddIdentityInfrastructure(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    await DbSeeder.SeedAsync(dbContext);

    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    await IdentitySeeder.SeedAsync(roleManager, userManager);
}

app.Run();