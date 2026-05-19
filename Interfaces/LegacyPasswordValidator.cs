using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ServicioRESTEjecucionComandos.Interfaces;

public class LegacyPasswordValidator : IPasswordValidator
{
    private static readonly byte[] Key =
    [
        219, 59, 110, 192, 231, 239, 27, 74,
        108, 154, 242, 210, 199, 65, 133, 114,
        170, 100, 184, 154, 189, 22, 6, 32
    ];

    private static readonly byte[] IV =
    [
        47, 72, 205, 209, 109, 48, 133, 203
    ];

    public Task<bool> ValidateAsync(string storedPassword, string inputPassword)
    {
        if (string.IsNullOrEmpty(storedPassword) || inputPassword == null)
            return Task.FromResult(false);

        var encryptedInput = Encrypt(inputPassword);

        return Task.FromResult(
            string.Equals(
                storedPassword,
                encryptedInput,
                StringComparison.Ordinal));
    }

    private static string Encrypt(string plainText)
    {
        using var tripleDes = TripleDES.Create();

        tripleDes.Key = Key;
        tripleDes.IV = IV;
        tripleDes.Mode = CipherMode.CBC;
        tripleDes.Padding = PaddingMode.PKCS7;

        byte[] plainBytes = Encoding.Unicode.GetBytes(plainText);

        using var memoryStream = new MemoryStream();

        using (var cryptoStream = new CryptoStream(
                   memoryStream,
                   tripleDes.CreateEncryptor(),
                   CryptoStreamMode.Write))
        {
            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
            cryptoStream.FlushFinalBlock();
        }

        return Convert.ToBase64String(memoryStream.ToArray());
    }
}