using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PulseORM.DemoEntities.Dtos;
using PulseORM.DemoEntities.Tables;
using PulseORM.DemoService;

namespace PulseORM.DemoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly IUserService _userService;
        public TestController(ICompanyService companyService, IUserService userService)
        {
            _companyService = companyService;
            _userService = userService;
        }
        [HttpGet("All Companies")]
        public async Task<IEnumerable<Company>> Get()
        {
            return await _companyService.GetCompanies();
        }
        
        [HttpGet("Filter")]
        public async Task<IEnumerable<Company>> Filter([FromQuery] Company filter)
        {
            return await _companyService.GetCompaniesFilter(filter);
        }
        
        [HttpGet("Pagination")]
        public async Task<ActionResult<CompanyPagedResponse>> GetCompaniesFilterPagination([FromQuery] CompanyPagination filter)
        {
            var (companies, totalCount) = await _companyService.GetCompaniesFilterPagination(filter);

            return Ok(new CompanyPagedResponse
            {
                Companies = companies,
                TotalCount = totalCount
            });
        }
        
        [HttpPost("UserAdd")]
        public async Task<Users> UserAdd([FromBody] Users user)
        {
            var userAdd = await _userService.UserAdd(user);
            if (userAdd>0)
            {
                return user;
            }
            else
            {
                throw new Exception("UserAdd Failed");
            }
        }
        
        [HttpPost("CompanyAdd")]
        public async Task<Company> CompanyAdd([FromBody] Company company)
        {
            var companyAdd = await _companyService.AddCompanyAsync(company);
            if (companyAdd>0)
            {
                return company;
            }
            else
            {
                throw new Exception("UserAdd Failed");
            }
        }

        [HttpPost("UserMultiAdd")]
        public async Task<int> UserMultiAdd([FromBody] IList<Users> users)
        {
            var userAdd = await _userService.BulkInsertAsync(users);
            if (userAdd > 0)
            {
                return userAdd;
            }
            else
            {
                throw new Exception("UserAdd Failed");
            }
        }
        
        [HttpGet("UserFindById")]
        public async Task<Users> UserFindById(long id)
        {
            return await _userService.GetUserByIdAsync(id);
        }
        
        [HttpPut("UserUpdate")]
        public async Task<int> UserUpdate(Users user)
        {
            return await _userService.UpdateUserAsync(user);
        }
        
        [HttpGet("UserListWithCompany")]
        public async Task<IEnumerable<Users>> UserListWithCompany()
        {
            return await _userService.GetUsersWithCompanyAsync();
        }
        
        [HttpGet("TestSqlQuery")]
        public async Task<IEnumerable<Company>> TestSqlQuery()
        {
            return await _companyService.TestSqlQueryAsync();
        }
        
        [HttpGet("GetAllUser")]
        public async Task<IEnumerable<Users>> GetAllUsers()
        {
            return await _userService.GetAllUserAsync();
        }
        
    }
}
