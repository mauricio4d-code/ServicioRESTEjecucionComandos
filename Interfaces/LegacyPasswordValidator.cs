using System.Security.Cryptography;
using System.Text;

namespace ServicioRESTEjecucionComandos.Interfaces;

public class LegacyPasswordValidator : IPasswordValidator
{
    public Task<bool> ValidateAsync(string storedPassword, string inputPassword)
    {
        if (storedPassword is null)
            throw new ArgumentNullException(nameof(storedPassword));
        if (inputPassword is null)
            throw new ArgumentNullException(nameof(inputPassword));

        string hashedInput = ComputeMd5(inputPassword);

        bool isValid = string.Equals(
            storedPassword,
            hashedInput,
            StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(isValid);
    }

    private static string ComputeMd5(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] hash = MD5.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}