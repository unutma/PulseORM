using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PulseORM.DemoEntities.Tables;

[Table("company")]
public class Company
{
    [Key] 
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}