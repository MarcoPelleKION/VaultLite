using System.Security.Cryptography;
using VaultLite.Api.Crypto;

namespace VaultLite.Api.Bootstrap;

public static class CryptoEndpoints
{
    public static void MapCryptoEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/crypto").RequireRateLimiting(RateLimitingSetup.CryptoPolicy);

        api.MapGet("/generate-key", () => Results.Ok(new { key = AesGcmCrypto.GenerateKey() }));

        api.MapPost("/encrypt", (CryptoRequest request) =>
        {
            try
            {
                var result = AesGcmCrypto.Encrypt(request.Key, request.Value);
                return Results.Ok(new { result });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPost("/decrypt", (CryptoRequest request) =>
        {
            try
            {
                var result = AesGcmCrypto.Decrypt(request.Key, request.Value);
                return Results.Ok(new { result });
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or CryptographicException)
            {
                return Results.BadRequest(new { error = "Impossibile decifrare: chiave o valore non validi." });
            }
        });
    }
}
