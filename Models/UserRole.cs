using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServicioRESTEjecucionComandos.Models;

/// <summary>
/// Legacy UserRole entity mapped to the existing "userrole" table.
/// This table cannot be modified - read-only mapping via EF Core.
/// </summary>
[Table("userrole")]
public class UserRole
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
