using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoEntities.Dtos;

public class CompanyPagination
{
    public Company? Company { get; set; } = null;
    public int Page  {get; set; } = 1;
    public int PageSize  {get; set; } = 1;
}