using System.ComponentModel.DataAnnotations;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using fim_queueing_admin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Action = fim_queueing_admin.Auth.Action;

namespace fim_queueing_admin.Controllers;

[AuthorizeOperation(Action.ViewCart)]
[Route("[controller]")]
public class CartController(FimDbContext dbContext) : Controller
{
    [HttpGet]
    public ActionResult Index()
    {
        return View();
    }
    
    [AuthorizeOperation(Action.ManageCart)]
    [HttpGet("[action]/{id:guid}")]
    public ActionResult Manage(Guid id)
    {
        ViewData["id"] = id;
        return View();
    }
    
    [AuthorizeOperation(Action.CreateCart)]
    [HttpGet("[action]")]
    public ActionResult Manage()
    {
        return View();
    }

    [AuthorizeOperation(Action.ManageCart)]
    [HttpPost("[action]/{id:guid}")]
    public async Task<ActionResult> Manage(Guid id, [FromForm] CartManageModel model)
    {
        if (!ModelState.IsValid) return View("Manage", model);

        var dbCart = await dbContext.Carts.SingleOrDefaultAsync(c => c.Id == id);
        if (dbCart is null) return NotFound();
        dbCart.Name = model.Name!;
        dbCart.TeamviewerId = model.TeamViewerId;
        dbCart.Configuration ??= new();
        dbCart.Configuration.StreamInfo = model.StreamInfos.Select((i, idx) => new CartStreamInfo
        {
            CartId = dbCart.Id,
            Index = idx,
            RtmpKey = i.RtmpKey,
            RtmpUrl = i.RtmpUrl,
            Enabled = i.Enabled
        }).ToList();

        await dbContext.SaveChangesAsync();
        
        return RedirectToAction(nameof(Index));
    }
    
    [AuthorizeOperation(Action.CreateCart)]
    [HttpPost("[action]")]
    public async Task<ActionResult> Manage([FromForm] CartManageModel model)
    {
        if (!ModelState.IsValid) return View("Manage", model);

        var cartId = Guid.NewGuid();
        var dbCart = new Cart
        {
            Id = cartId,
            Configuration = new Cart.AvCartConfiguration
            {
                AuthToken = Guid.NewGuid().ToString(),
                StreamInfo = model.StreamInfos.Select((i, idx) => new CartStreamInfo
                {
                    CartId = cartId,
                    Index = idx,
                    RtmpKey = i.RtmpKey,
                    RtmpUrl = i.RtmpUrl,
                    Enabled = i.Enabled
                }).ToList()
            },
            Name = model.Name!,
            TeamviewerId = model.TeamViewerId,
        };
        await dbContext.Carts.AddAsync(dbCart);
        await dbContext.SaveChangesAsync();
        
        return RedirectToAction(nameof(Index));
    }
    
    public class CartManageModel
    {
        [MaxLength(255)]
        [Required]
        public string? Name { get; set; }
        
        [MaxLength(16)]
        public string? TeamViewerId { get; set; }

        public List<StreamInfoManageModel> StreamInfos { get; set; } = [];

        public CartManageModel()
        {
        }
        
        public CartManageModel(Cart dbModel)
        {
            Name = dbModel.Name;
            TeamViewerId = dbModel.TeamviewerId;
            StreamInfos = dbModel.Configuration?.StreamInfo?.Select(i => new StreamInfoManageModel
            {
                RtmpUrl = i.RtmpUrl,
                RtmpKey = i.RtmpKey,
                Enabled = i.Enabled
            }).ToList() ?? [];
        }
    }

    public class StreamInfoManageModel
    {
        [MaxLength(512)]
        public string? RtmpUrl { get; set; }
        
        [MaxLength(512)]
        public string? RtmpKey { get; set; }

        public bool Enabled { get; set; } = true;
    }
}