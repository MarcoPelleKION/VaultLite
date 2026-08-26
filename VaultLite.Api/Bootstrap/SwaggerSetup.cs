namespace VaultLite.Api.Bootstrap;

public static class SwaggerSetup
{
    public static void UseApiDocs(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "VaultLite API v1");
        });
    }
}
