using PulseORM.DemoDataLayer;

namespace PulseORM.DemoService;

public interface ICompanyService
{
    Task<IList<Company>> GetCompanies();
}