using fim_queueing_admin.Data;
using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    
    [HttpPut("PushStreamKeys/{cartId:guid:required}")]
    public async Task<Results<Ok, NotFound>> PushStreamKeys([FromRoute] Guid cartId, [FromServices] FimDbContext dbContext)
    {
        var cart = await dbContext.Carts.FirstOrDefaultAsync(c => c.Id == cartId);
        if (cart is null) return TypedResults.NotFound();
        
        await assistantService.PushStreamKeys(cart);
        return TypedResults.Ok();
    }
    
    [HttpGet("VmixConfig/{cartId:guid:required}")]
    public async Task<Results<Ok<string>, NoContent>> GetVmixConfig([FromRoute] Guid cartId)
    {
        try
        {
            var resp = await assistantService.GetVmixConfig(cartId);
            if (resp is null) return TypedResults.NoContent();
            return TypedResults.Ok(resp);
        }
        catch (OperationCanceledException)
        {
            return TypedResults.NoContent();
        }
    }
}