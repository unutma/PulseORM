using System.Linq.Expressions;
using PulseORM.Core;
using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public class CompanyService : ICompanyService
{
    private readonly IAppDb _appDb;
    
    public CompanyService(IAppDb appDb)
    {
        _appDb = appDb;
    }
    public async Task<IList<Company>> GetCompanies()
    {
        return await _appDb.GetAllAsync<Company>();
    }

    public async Task<IEnumerable<Company>> GetCompaniesFilter(Company filter)
    {
        return  await _appDb.Query<Company>().FilterSql(s=>s.CompanyId > filter.CompanyId).ToListAsync();
    }
}