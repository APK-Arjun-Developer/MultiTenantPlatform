using Api.Middleware;
using Api.Options;

namespace Api.Extensions;

public static class SwaggerApplicationBuilderExtensions
{
    public static bool IsSwaggerEnabled(this WebApplication app, IConfiguration configuration)
    {
        if (app.Environment.IsDevelopment())
        {
            return true;
        }

        return configuration.GetValue($"{SwaggerAccessOptions.SectionName}:EnabledInProduction", false);
    }

    public static void UseSwaggerDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseMiddleware<SwaggerGateMiddleware>();
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.EnableTryItOutByDefault();
            options.EnablePersistAuthorization();
            options.InjectJavascript("/swagger-ui/swagger-auto-auth.js");
            options.RoutePrefix = "swagger";
        });
    }
}
