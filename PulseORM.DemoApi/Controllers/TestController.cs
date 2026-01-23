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
        [HttpGet("Test")]
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
    }
}
