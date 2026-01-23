using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PulseORM.DemoEntities.Tables;

[Table("users")]
public sealed class Users
{
    [Key]
    public int UserId { get; set; }
    
    public string FirstName { get; set; } = null!;
    
    public string LastName { get; set; } = null!;
    
    public string? Title { get; set; }
    
    public int CompanyId { get; set; }
}