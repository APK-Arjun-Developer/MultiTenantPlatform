using Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult OkEnvelope<T>(T data, string message = "Success")
    {
        return Ok(new ApiEnvelope<T>
        {
            Data = data,
            Message = message,
            TraceId = HttpContext.TraceIdentifier,
        });
    }

    protected IActionResult OkEnvelope(string message)
    {
        return Ok(new ApiEnvelope<object?>
        {
            Data = null,
            Message = message,
            TraceId = HttpContext.TraceIdentifier,
        });
    }
}

