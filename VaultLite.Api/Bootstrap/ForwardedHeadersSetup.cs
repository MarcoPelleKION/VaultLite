using Microsoft.AspNetCore.HttpOverrides;

namespace VaultLite.Api.Bootstrap;

public static class ForwardedHeadersSetup
{

    //X-Forwarded-Host/Proto - l'app crede di essere chiamata dall'hostname pubblico non dal worker cloudflare
    public static void UseProxyForwardedHeaders(this WebApplication app)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
        };

        //mi fido di ogni ip - qua non un rischio 
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        app.UseForwardedHeaders(options);
    }
}
