using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/test")]
public class TestController : ControllerBase
{
    [Authorize]
    [HttpGet("secure")]
    public IActionResult Secure()
    {
        return Ok(new
        {
            Message = "JWT Authentication Working"
        });
    }
}