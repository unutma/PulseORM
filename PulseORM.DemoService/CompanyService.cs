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
        
        var list = await _appDb.Query<Company>()
        .ToListSelectAsync<CompanyNameDto>();

        return await _appDb.GetAllAsync<Company>();
    }

    public async Task<IEnumerable<Company>> GetCompaniesFilter(Company filter)
    {
        return await _appDb.Query<Company>().FilterSql(s=>s.CompanyName.Contains(filter.CompanyName)).ToListAsync();
    }
    public async Task<(IEnumerable<Company> Companies, long TotalCount)> GetCompaniesFilterPagination(CompanyPagination filter)
    {
        Expression<Func<Company, bool>> where = s => true;

        if (filter.Company?.CompanyId > 0)
        {
            // where =  s => s.CompanyId > filter.Company.CompanyId;
        }
        
        if (filter.Company?.CompanyName is not null)
        {
            // where =  s => s.CompanyName.StartsWith(filter.Company.CompanyName);
            // where =  s => s.CompanyName.Contains(filter.Company.CompanyName);
            where =  s => s.CompanyName.EndsWith(filter.Company.CompanyName);
        }
       

        var (companies, totalCount) = await _appDb.GetAllPagedAsync<Company>(
            filter.Page,
            filter.PageSize,
            s => s.CompanyId,
            true,
            whereInclude: where
        );


        return (companies, totalCount);
    }

    public async Task<IList<Company>> TestSqlQueryAsync()
    {
        var testCount = this.TestSqlQueryReturnCount();
        if (testCount.Result > 3)
        {
            Console.WriteLine("Success");
        }
        else
        {
            Console.WriteLine("Fail");
        }
        return await _appDb.SqlQuery<Company>("SELECT * FROM Company").FilterSql(s=>s.CompanyId >2).ToListAsync();
        // return await _appDb.SqlQuery<Company>("select * from Company").ToListAsync();
    }

    public async Task<int> AddCompanyAsync(Company company)
    {
        return await _appDb.InsertAsync(company);
    }

    private async Task<long> TestSqlQueryReturnCount()
    {
        var count = await  _appDb.QueryCountSqlCoreAsync("Select Count(*) from Company", null);
        var countWithParam = await _appDb.QueryCountSqlCoreAsync(
            "SELECT COUNT(*) FROM Company WHERE IsActive = @isActive",
            new Dictionary<string, object?>
            {
                ["isActive"] = true
            }
        );

        return count;
    }
}