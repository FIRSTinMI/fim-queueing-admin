using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using fim_queueing_admin.Data;
using fim_queueing_admin.Hubs;
using fim_queueing_admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace fim_queueing_admin.Controllers;

[Authorize]
[Route("[controller]")]
public partial class AlertController(FimDbContext dbContext, IHubContext<AssistantHub> assistantHub) : Controller
{
    
    [HttpGet]
    public ActionResult Index()
    {
        return View();
    }
    
    [HttpGet("[action]/{id:guid}")]
    public ActionResult Manage(Guid id)
    {
        ViewData["id"] = id;
        return View();
    }
    
    [HttpGet("[action]")]
    public ActionResult Manage()
    {
        return View();
    }

    [HttpPost("[action]/{id:guid}")]
    public async Task<ActionResult> Manage(Guid id, [FromForm] AlertManageModel model)
    {
        var dbAlert = await dbContext.Alerts.Include(a => a.AlertCarts).SingleOrDefaultAsync(a => a.Id == id);
        if (dbAlert is null) return NotFound();
        dbAlert.Content = model.Content;

        foreach (var addedCart in model.SelectedCartIds.ExceptBy(dbAlert.AlertCarts!.Select(ac => ac.CartId), g => g))
        {
            dbAlert.AlertCarts!.Add(new AlertCart
            {
                AlertId = id,
                CartId = addedCart
            });
        }

        foreach (var removedCart in dbAlert.AlertCarts!.ExceptBy(model.SelectedCartIds, ac => ac.CartId))
        {
            dbAlert.AlertCarts!.Remove(removedCart);
        }

        await dbContext.SaveChangesAsync();
        
        await assistantHub.SendPendingAlertsToEveryone(dbContext);
        
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("[action]")]
    public async Task<ActionResult> Manage([FromForm] AlertManageModel model)
    {
        var dbAlert = new Alert
        {
            Id = Guid.NewGuid(),
            Content = model.Content
        };
        await dbContext.Alerts.AddAsync(dbAlert);
        await dbContext.SaveChangesAsync();

        await assistantHub.SendPendingAlertsToEveryone(dbContext);
        
        return RedirectToAction(nameof(Index));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Task.WhenAll(
            dbContext.Alerts.Where(a => a.Id == id).ExecuteDeleteAsync(), 
            dbContext.AlertCarts.Where(ac => ac.AlertId == id).ExecuteDeleteAsync()
        );

        await assistantHub.SendPendingAlertsToEveryone(dbContext);

        return RedirectToAction(nameof(Index));
    }
    
    public class AlertManageModel
    {
        [Required]
        public required string Content { get; set; }

        public List<Guid> SelectedCartIds { get; set; } = [];
    }

    [GeneratedRegex("<(.|\n)*?>")]
    public static partial Regex HtmlTagRegex();
}