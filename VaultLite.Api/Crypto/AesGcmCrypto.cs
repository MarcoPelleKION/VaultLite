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
