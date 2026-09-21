using Microsoft.AspNetCore.Mvc;

namespace smartHRMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(new
        {
            message = "Employee API is working"
        });
    }
}