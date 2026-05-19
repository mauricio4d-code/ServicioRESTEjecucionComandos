namespace ServicioRESTEjecucionComandos.Interfaces;

/// <summary>
/// Interface for password validation against the legacy password storage.
/// The client must implement this interface with their legacy password comparison logic.
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// Validates the input password against the stored (encrypted/hashed) password.
    /// </summary>
    /// <param name="storedPassword">The password stored in the legacy database.</param>
    /// <param name="inputPassword">The password provided by the user during login.</param>
    /// <returns>True if the password is valid, false otherwise.</returns>
    Task<bool> ValidateAsync(string storedPassword, string inputPassword);
}
