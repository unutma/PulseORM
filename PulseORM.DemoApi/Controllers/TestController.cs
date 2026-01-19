using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PulseORM.DemoEntities.Tables;
using PulseORM.DemoService;

namespace PulseORM.DemoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly ICompanyService _companyService;
        public TestController(ICompanyService companyService)
        {
            _companyService = companyService;
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

    }
}
