using System.ComponentModel.DataAnnotations;
using fim_queueing_admin.Data;
using fim_queueing_admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
[Route("[controller]")]
public class CartController(FimDbContext dbContext) : Controller
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
    public async Task<ActionResult> Manage(Guid id, [FromForm] CartManageModel model)
    {
        var dbCart = await dbContext.Carts.FindAsync(id);
        if (dbCart is null) return NotFound();
        dbCart.Name = model.Name;
        dbCart.TeamViewerId = model.TeamViewerId;

        await dbContext.SaveChangesAsync();
        
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("[action]")]
    public async Task<ActionResult> Manage([FromForm] CartManageModel model)
    {
        var dbCart = new Cart
        {
            Id = Guid.NewGuid(),
            AuthToken = Guid.NewGuid().ToString(),
            Name = model.Name,
            TeamViewerId = model.TeamViewerId,
        };
        await dbContext.Carts.AddAsync(dbCart);
        await dbContext.SaveChangesAsync();
        
        return RedirectToAction(nameof(Index));
    }
    
    public class CartManageModel
    {
        [MaxLength(255)]
        [Required]
        public required string Name { get; set; }
        
        [MaxLength(16)]
        public string? TeamViewerId { get; set; }
    }
}