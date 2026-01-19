using System.Linq.Expressions;
using PulseORM.Core;
using PulseORM.DemoEntities.Dtos;
using PulseORM.DemoEntities.Tables;

namespace PulseORM.DemoService;

public interface ICompanyService
{
    Task <IList<Company>> GetCompanies();
    Task <IEnumerable<Company>> GetCompaniesFilter(Company filter); 
    Task<(IEnumerable<Company> Companies, long TotalCount)> GetCompaniesFilterPagination(CompanyPagination filter);

}