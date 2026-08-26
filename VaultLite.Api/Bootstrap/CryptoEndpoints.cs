using VaultLite.Api.Crypto;

namespace VaultLite.Api.Bootstrap;

public static class CryptoEndpoints
{
    public static void MapCryptoEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("").RequireRateLimiting(RateLimitingSetup.CryptoPolicy);

        api.MapGet("/key", () => Results.Ok(new { key = AesGcmCrypto.GenerateKey() }));

        api.MapPost("/encrypt", (CryptoRequest request) =>
            Results.Ok(new { result = AesGcmCrypto.Encrypt(request.Key, request.Value) }));

        api.MapPost("/decrypt", (CryptoRequest request) =>
            Results.Ok(new { result = AesGcmCrypto.Decrypt(request.Key, request.Value) }));
    }
}
