namespace Arkanis.Template.Service.Controllers;

using Arkanis.Template.Contracts;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Reports the service's public status contract.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    /// <summary>
    /// Gets the current service status.
    /// </summary>
    /// <returns>The healthy service status.</returns>
    [HttpGet]
    [ProducesResponseType<ServiceStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<ServiceStatusResponse> Get() => Ok(new ServiceStatusResponse("Healthy"));
}
