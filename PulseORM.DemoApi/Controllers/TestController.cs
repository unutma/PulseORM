using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PulseORM.DemoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public ActionResult Get()
        {
            return Ok("Hello World!");
        }

    }
}
