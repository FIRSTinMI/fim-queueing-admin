using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using softaware.Authentication.Hmac;

namespace fim_queueing_admin.Controllers;

[Authorize(HmacAuthenticationDefaults.AuthenticationScheme)]
[Route("[controller]")]
public class AssistantController(AssistantService assistantService) : Controller
{
    [HttpPut("StartStream/{cartId:guid:required}")]
    public async Task<Ok> StartStream([FromRoute] Guid cartId, [FromQuery] int? streamNum = null)
    {
        await assistantService.StartStreamForCart(cartId, streamNum);
        return TypedResults.Ok();
    }
    
    [HttpPut("StopStream/{cartId:guid:required}")]
    public async Task<Ok> StopStream([FromRoute] Guid cartId, [FromQuery] int? streamNum = null)
    {
        await assistantService.StopStreamForCart(cartId, streamNum);
        return TypedResults.Ok();
    }
}