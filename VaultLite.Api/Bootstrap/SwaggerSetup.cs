namespace VaultLite.Api.Bootstrap;

public static class SwaggerSetup
{
    public static void UseDevelopmentApiDocs(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "VaultLite API v1");
        });
    }
}
