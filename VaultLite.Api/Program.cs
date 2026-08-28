using VaultLite.Api.Bootstrap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCryptoRateLimiting();

var app = builder.Build();

app.UseApiErrorHandling();
app.UseProxyForwardedHeaders();
app.UseApiDocs();

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapCryptoEndpoints();

app.Run();
