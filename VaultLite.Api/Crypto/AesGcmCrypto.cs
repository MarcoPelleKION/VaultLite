using System.Security.Cryptography;
using System.Text;

namespace VaultLite.Api.Crypto;

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
        if (plainText is null)
            throw new CryptoException("Testo in chiaro mancante.");

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
        // Qualsiasi causa di fallimento (chiave errata, base64 invalido, testo manomesso)
        // viene volutamente collassata in un unico messaggio generico, per non offrire
        // a un chiamante malevolo un oracolo su quale parte dell'input sia sbagliata.
        try
        {
            var key = DecodeKey(keyBase64);
            if (cipherTextBase64 is null)
                throw new FormatException();

            var raw = Convert.FromBase64String(cipherTextBase64);
            if (raw.Length < NonceSizeBytes + TagSizeBytes)
                throw new CryptographicException();

            var nonce = raw[..NonceSizeBytes];
            var tag = raw[^TagSizeBytes..];
            var cipherText = raw[NonceSizeBytes..^TagSizeBytes];

            var plainBytes = new byte[cipherText.Length];
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, cipherText, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex) when (ex is CryptoException or FormatException or CryptographicException)
        {
            throw new CryptoException("Impossibile decifrare: chiave o valore non validi.");
        }
    }

    private static byte[] DecodeKey(string keyBase64)
    {
        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new CryptoException("Chiave mancante.");

        byte[] key;
        try { key = Convert.FromBase64String(keyBase64); }
        catch (FormatException) { throw new CryptoException("Chiave non in formato Base64 valido."); }

        if (key.Length != KeySizeBytes)
            throw new CryptoException($"Chiave non valida: attesi {KeySizeBytes} byte, ricevuti {key.Length}.");

        return key;
    }
}
