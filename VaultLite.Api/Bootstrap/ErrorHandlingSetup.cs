using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using VaultLite.Api.Crypto;

namespace VaultLite.Api.Bootstrap;

public static class ErrorHandlingSetup
{
    public static void UseApiErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var (statusCode, message) = error switch
                {
                    CryptoException ex => (StatusCodes.Status400BadRequest, ex.Message),
                    BadHttpRequestException => (StatusCodes.Status400BadRequest,
                        "Richiesta non valida: body mancante o non in formato JSON corretto."),
                    _ => (StatusCodes.Status500InternalServerError, "Errore interno del server.")
                };

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
            });
        });
    }
}
