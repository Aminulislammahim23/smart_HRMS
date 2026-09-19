using Microsoft.AspNetCore.Mvc;

namespace smartHRMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetHealthStatus()
        {
            return Ok(new
             { 
                status = "Healthy",
                application = "smartHRMS",
                version = "1.0.0",
             });
        }
    }
}