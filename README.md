# VaultLite

VaultLite è una web app .NET 10 per cifrare e decifrare stringhe al volo con AES-256-GCM, senza salvare alcun dato lato server. Nasce come strumento generico per gestire segreti (API key, password, connection string) usati in altri progetti, ma è pensata per essere riutilizzabile per qualsiasi esigenza futura di encrypt/decrypt.

Questo documento descrive l'architettura in modo completo.

## 1. Panoramica

VaultLite espone una piccola API REST per cifrare/decifrare testo con AES-256-GCM, più un frontend statico a singola pagina servito dalla stessa applicazione. Le caratteristiche chiave:

- **nessuna chiave hardcoded o persistita lato server**: la chiave AES viene fornita dal client ad ogni richiesta e vive solo per la durata della singola operazione;
- **cifratura indipendente dall'hardware**: nessuna dipendenza da DPAPI, TPM o keychain della macchina — solo AES puro, utilizzabile da qualsiasi browser/dispositivo verso lo stesso endpoint;
- **formato standard e documentato**: qualsiasi altro sistema che implementi lo stesso schema AES-256-GCM (sezione 5) può cifrare/decifrare valori compatibili con VaultLite, senza conversioni.

## 2. Flusso applicativo

```
Client (browser) -> VaultLite API -> risposta cifrata/decifrata -> Client (browser)
```

Per ogni richiesta di encrypt/decrypt, VaultLite:

1. valida che la chiave AES fornita dal client sia una stringa Base64 di 32 byte;
2. esegue l'operazione richiesta (cifratura o decifratura) interamente in memoria;
3. restituisce il risultato al client, senza salvare né il valore in chiaro né quello cifrato né la chiave.

Non esiste alcuno storico delle operazioni: VaultLite è stateless per design.

## 3. Endpoint esposti

| # | Metodo | Path        | Descrizione                                              |
|---|--------|-------------|-----------------------------------------------------------|
| 1 | POST   | `/encrypt`  | Cifra un testo in chiaro con la chiave fornita             |
| 2 | POST   | `/decrypt`  | Decifra un testo cifrato con la chiave fornita             |
| 3 | GET    | `/key`      | Genera una nuova chiave AES-256 casuale (Base64, 32 byte)  |

### Request/response — `/encrypt` e `/decrypt`

Request:
```json
{
  "key": "AfKFSltBBSJ6UJoE4SKDG1AZUFx0SbLfEW/hjYUNMZY=",
  "value": "testo in chiaro o cifrato, a seconda dell'endpoint"
}
```

Response (200):
```json
{
  "result": "valore cifrato o decifrato"
}
```

Response di errore (400) — chiave non valida, valore non nel formato atteso, o decifratura fallita (chiave errata o testo manomesso):
```json
{
  "error": "descrizione sintetica dell'errore"
}
```

### Response — `/key`

```json
{
  "key": "nuova chiave Base64 a 32 byte"
}
```

## 4. Implementazione con Minimal API

Un unico endpoint group (path root, nessun prefisso), nessun controller MVC. Nessun try/catch negli endpoint: la logica di dominio lancia `CryptoException` con un messaggio già sicuro da esporre, e un exception handler centralizzato (sezione 4.1) lo traduce nella risposta HTTP.

```csharp
var api = app.MapGroup("").RequireRateLimiting(RateLimitingSetup.CryptoPolicy);

api.MapGet("/key", () => Results.Ok(new { key = AesGcmCrypto.GenerateKey() }));

api.MapPost("/encrypt", (CryptoRequest request) =>
    Results.Ok(new { result = AesGcmCrypto.Encrypt(request.Key, request.Value) }));

api.MapPost("/decrypt", (CryptoRequest request) =>
    Results.Ok(new { result = AesGcmCrypto.Decrypt(request.Key, request.Value) }));

record CryptoRequest(string Key, string Value);
```

### 4.1 Gestione errori centralizzata

Tutte le eccezioni attraversano un unico exception handler globale (`app.UseExceptionHandler(...)`), registrato per primo nella pipeline:

```csharp
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
```

`CryptoException` è l'unico tipo di eccezione che la logica crypto può lanciare: su `/decrypt`, qualunque causa di fallimento (chiave errata, Base64 invalido, testo manomesso) viene volutamente collassata in un unico messaggio generico, per non fornire a un chiamante malevolo un oracolo su quale parte dell'input sia sbagliata.

## 5. Crittografia

Cifratura simmetrica **AES-256-GCM**, tramite `System.Security.Cryptography.AesGcm` incluso nel framework — nessuna libreria esterna.

### Formato

Ogni valore cifrato è una stringa Base64 che codifica, concatenati: **nonce (12 byte) + ciphertext + tag di autenticazione (16 byte)**. Lo schema è standard e documentato qui appositamente: chiunque debba integrare la cifratura/decifratura in un altro sistema (es. leggere a runtime un segreto cifrato con VaultLite) può reimplementarlo autonomamente seguendo questa specifica, senza dipendere da VaultLite stesso.

### Motivazione della scelta "chiave lato client"

VaultLite è un tool generico multi-uso: non ha senso avere una chiave fissa, perché progetti diversi useranno chiavi diverse. La chiave viene quindi sempre fornita dal chiamante e mai persistita né loggata lato server.

### Implementazione

```csharp
public static class AesGcmCrypto
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    public static string GenerateKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeySizeBytes));

    public static string Encrypt(string keyBase64, string plainText)
    {
        var key = DecodeKey(keyBase64);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherText = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipherText, tag);

        var raw = new byte[NonceSizeBytes + cipherText.Length + TagSizeBytes];
        nonce.CopyTo(raw, 0);
        cipherText.CopyTo(raw, NonceSizeBytes);
        tag.CopyTo(raw, NonceSizeBytes + cipherText.Length);

        return Convert.ToBase64String(raw);
    }

    public static string Decrypt(string keyBase64, string cipherTextBase64)
    {
        var key = DecodeKey(keyBase64);
        var raw = Convert.FromBase64String(cipherTextBase64);
        if (raw.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Valore cifrato non valido: lunghezza insufficiente.");

        var nonce = raw[..NonceSizeBytes];
        var tag = raw[^TagSizeBytes..];
        var cipherText = raw[NonceSizeBytes..^TagSizeBytes];

        var plainBytes = new byte[cipherText.Length];
        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, cipherText, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DecodeKey(string keyBase64)
    {
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new ArgumentException("Chiave mancante.", nameof(keyBase64));

        byte[] key;
        try { key = Convert.FromBase64String(keyBase64); }
        catch (FormatException) { throw new ArgumentException("Chiave non in formato Base64 valido.", nameof(keyBase64)); }

        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Chiave non valida: attesi {KeySizeBytes} byte, ricevuti {key.Length}.", nameof(keyBase64));

        return key;
    }
}
```

## 6. Frontend

Pagina statica singola (`wwwroot/index.html` + `css/site.css` + `js/app.js`, JS vanilla, styling con Bootstrap via CDN), servita direttamente dalla stessa applicazione ASP.NET Core: stesso dominio dell'API, nessun CORS da configurare, un solo artefatto da deployare.

Funzionalità della pagina:

- campo per la chiave AES, con pulsante "Genera nuova chiave" (chiama `/key`);
- textarea di input (testo in chiaro o cifrato) e textarea di output, con pulsante "Copia risultato";
- pulsante opzionale "Ricorda su questo browser": salva la chiave in `localStorage`, **mai inviata altrove** se non verso l'API stessa.

## 7. Struttura del progetto

```
VaultLite.Api/
  Program.cs                        -> bootstrap: wiring dei moduli, pipeline dei middleware
  Bootstrap/
    RateLimitingSetup.cs            -> policy di rate limiting (30 req/min per IP)
    ErrorHandlingSetup.cs           -> exception handler centralizzato -> risposta HTTP
    SwaggerSetup.cs                 -> OpenAPI + Swagger UI
    CryptoEndpoints.cs              -> route REST (/key, /encrypt, /decrypt)
  Crypto/
    AesGcmCrypto.cs                 -> Encrypt / Decrypt / GenerateKey
    CryptoException.cs              -> unica eccezione "safe to expose" dal dominio crypto
    CryptoRequest.cs                -> record del body JSON (Key, Value)
  wwwroot/
    index.html                      -> Frontend statico (markup, Bootstrap via CDN)
    css/
      site.css                      -> Stili custom applicativi
    js/
      app.js                        -> Logica frontend (fetch verso /key, /encrypt, /decrypt)
```

## 8. Stack tecnologico

| Ambito         | Tecnologia                                                          |
|----------------|----------------------------------------------------------------------|
| Framework      | .NET 10 / ASP.NET Core                                               |
| API            | Minimal API, route group unico                                       |
| Crittografia   | AES-256-GCM (`System.Security.Cryptography`, incluso nel framework)  |
| Frontend       | HTML/JS vanilla + Bootstrap (CDN), servito da `wwwroot` (static files middleware) |
| Persistenza    | Nessuna — app stateless                                              |

Nessun pacchetto NuGet esterno richiesto oltre a quelli di default del template `webapi`.

## 9. Configurazione multi-ambiente

Standard ASP.NET Core: `appsettings.json` + `appsettings.Development.json` / `appsettings.Production.json`, selezionati da `ASPNETCORE_ENVIRONMENT`. Non ci sono opzioni specifiche da configurare: l'app non ha stato né segreti propri.

### CORS

Non necessario: frontend e API sono servite dalla stessa origine.

## 10. Rate limiting

Per proteggere la quota di CPU/giorno del piano free (sezione 11) da un uso anomalo o automatizzato, dato che l'API è pubblica e non richiede autenticazione, è applicato un rate limiting **per indirizzo IP** su tutti gli endpoint crypto, tramite il middleware nativo di ASP.NET Core (`Microsoft.AspNetCore.RateLimiting`, incluso nel framework, nessun pacchetto NuGet aggiuntivo).

- Algoritmo: **fixed window**, partizionato per IP del chiamante.
- Limite: **30 richieste al minuto** per IP — ampiamente sufficiente per uso interattivo (personale o di un collega), ma sufficiente a bloccare rapidamente uno script che martella l'endpoint.
- Nessuna coda (`QueueLimit = 0`): le richieste oltre il limite vengono rifiutate subito con **429**, non messe in attesa.
- Risposta di rifiuto in formato coerente con gli altri errori dell'API (`{ "error": "..." }`).

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("crypto", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            """{"error":"Troppe richieste, riprova tra qualche secondo."}""", token);
    };
});

app.UseRateLimiter();

var api = app.MapGroup("").RequireRateLimiting("crypto");
```

## 11. Deployment

**Hosting: Azure App Service, tier F1 (Free)** — scelto perché Vercel/Netlify non supportano il runtime ASP.NET Core nativamente (solo Node/Python/Go/Ruby/Rust), mentre F1 è gratuito a tempo indeterminato (non un trial), con limite di 60 minuti di CPU effettiva al giorno, ampiamente sufficiente per un tool a basso traffico come questo.

- Deploy tramite GitHub Actions (workflow standard `azure/webapps-deploy`) collegato al branch principale del repository, oppure deploy manuale da Visual Studio/CLI in fase iniziale.
- Nessun deployment self-contained necessario: App Service fornisce il runtime .NET, quindi è sufficiente una publish framework-dependent.
- Consigliato impostare un **budget alert** su Azure (soglia bassa, es. €0.01) come rete di sicurezza contro addebiti accidentali dovuti a risorse aggiuntive collegate per errore.

## 12. Punti aperti / da definire

- Dominio custom (facoltativo): al momento si usa il dominio di default `*.azurewebsites.net`.
