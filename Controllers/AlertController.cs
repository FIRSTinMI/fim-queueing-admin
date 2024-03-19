using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using fim_queueing_admin.Models;
using fim_queueing_admin.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Action = fim_queueing_admin.Auth.Action;

namespace fim_queueing_admin.Controllers;

[AuthorizeOperation(Action.ViewAlert)]
[Route("[controller]")]
public partial class AlertController(FimDbContext dbContext, AssistantService assistantService) : Controller
{
    
    [HttpGet]
    public ActionResult Index()
    {
        return View();
    }
    
    [AuthorizeOperation(Action.ManageAlert)]
    [HttpGet("[action]/{id:guid}")]
    public ActionResult Manage(Guid id)
    {
        ViewData["id"] = id;
        return View();
    }
    
    [AuthorizeOperation(Action.CreateAlert)]
    [HttpGet("[action]")]
    public ActionResult Manage()
    {
        return View();
    }

    [AuthorizeOperation(Action.ManageAlert)]
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
        
        await assistantService.SendPendingAlertsToEveryone();
        
        return RedirectToAction(nameof(Index));
    }
    
    [AuthorizeOperation(Action.CreateAlert)]
    [HttpPost("[action]")]
    public async Task<ActionResult> Manage([FromForm] AlertManageModel model)
    {
        var dbAlert = new Alert
        {
            Id = Guid.NewGuid(),
            Content = model.Content,
            AlertCarts = new List<AlertCart>(),
            CreatedAt = DateTime.UtcNow
        };
        
        foreach (var addedCart in model.SelectedCartIds)
        {
            dbAlert.AlertCarts!.Add(new AlertCart
            {
                AlertId = Guid.Empty,
                CartId = addedCart
            });
        }
        
        await dbContext.Alerts.AddAsync(dbAlert);
        await dbContext.SaveChangesAsync();

        await assistantService.SendPendingAlertsToEveryone();
        
        return RedirectToAction(nameof(Index));
    }

    [AuthorizeOperation(Action.Admin)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        await Task.WhenAll(
            dbContext.Alerts.Where(a => a.Id == id).ExecuteDeleteAsync(), 
            dbContext.AlertCarts.Where(ac => ac.AlertId == id).ExecuteDeleteAsync()
        );

        await assistantService.SendPendingAlertsToEveryone();

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