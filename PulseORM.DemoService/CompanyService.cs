using System.Linq.Expressions;
using PulseORM.Core;
using PulseORM.DemoEntities.Dtos;
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
        return await _appDb.Query<Company>().FilterSql(s=>s.CompanyId > filter.CompanyId).ToListAsync();
    }
    public async Task<(IEnumerable<Company> Companies, long TotalCount)> GetCompaniesFilterPagination(CompanyPagination filter)
    {
        var (companies, totalCount) = await _appDb.GetAllPagedAsync<Company>(
            filter.Page, filter.PageSize,
            s => s.CompanyId,
            true,
            whereInclude: s => s.CompanyId > filter.Company.CompanyId
        );

        return (companies, totalCount);
    }
}