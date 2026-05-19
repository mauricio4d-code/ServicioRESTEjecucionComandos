using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Legacy User entity mapped to the existing "user" table.
/// This table cannot be modified - read-only mapping via EF Core.
/// </summary>
[Table("user")]
public class User
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("password")]
    public string Password { get; set; } = string.Empty;

    [Column("firstname")]
    public string? Firstname { get; set; }

    [Column("lastname")]
    public string? Lastname { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("userroleid")]
    public int Userroleid { get; set; }

    [Column("userstate")]
    public string Userstate { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to UserRole.
    /// </summary>
    [ForeignKey("Userroleid")]
    public UserRole? UserRole { get; set; }
}
