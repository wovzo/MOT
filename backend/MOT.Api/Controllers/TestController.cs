using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;
namespace MOT.Api.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowAll")]
    public class TestController : ControllerBase {
        [HttpPost("register")]
        public IActionResult Post() => Ok("test");
    }
}
