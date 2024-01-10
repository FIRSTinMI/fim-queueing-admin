using fim_queueing_admin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

/// <summary>
/// This controller should only be called from FIM AV Assistant clients using mTLS
/// </summary>
[Authorize(AuthTokenScheme.AuthenticationScheme)]
public class AssistantController : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return Ok(User.FindFirst(ClaimTypes.CartId)?.Value);
    }
}