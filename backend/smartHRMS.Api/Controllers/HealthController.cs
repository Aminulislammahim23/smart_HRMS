using Microsoft.AspNetCore.Mvc;
using smartHRMS.Application.Common.Models;

namespace smartHRMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public ActionResult<ApiResponse<object>> GetHealthStatus()
        {
            return Ok(ApiResponse<object>.Ok(
                new
                {
                    status = "Healthy",
                    application = "smartHRMS",
                    version = "1.0.0",
                },
                "Service is healthy."));
        }
    }
}
