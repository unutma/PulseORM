using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public interface ICompanyService
{
    Task<IList<Company>> GetCompanies();
}