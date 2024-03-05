using System.ComponentModel.DataAnnotations;
using fim_queueing_admin.Data;
using fim_queueing_admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        if (!ModelState.IsValid) return View("Manage", model);

        var dbCart = await dbContext.Carts.Include(c => c.StreamInfos).SingleOrDefaultAsync(c => c.Id == id);
        if (dbCart is null) return NotFound();
        dbCart.Name = model.Name!;
        dbCart.TeamViewerId = model.TeamViewerId;
        dbCart.StreamInfos.Clear();
        dbCart.StreamInfos = model.StreamInfos.Select((i, idx) => new CartStreamInfo
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
    
    [HttpPost("[action]")]
    public async Task<ActionResult> Manage([FromForm] CartManageModel model)
    {
        if (!ModelState.IsValid) return View("Manage", model);

        var cartId = Guid.NewGuid();
        var dbCart = new Cart
        {
            Id = cartId,
            AuthToken = Guid.NewGuid().ToString(),
            Name = model.Name!,
            TeamViewerId = model.TeamViewerId,
            StreamInfos = model.StreamInfos.Select((i, idx) => new CartStreamInfo
            {
                CartId = cartId,
                Index = idx,
                RtmpKey = i.RtmpKey,
                RtmpUrl = i.RtmpUrl,
                Enabled = i.Enabled
            }).ToList()
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
            TeamViewerId = dbModel.TeamViewerId;
            StreamInfos = dbModel.StreamInfos.Select(i => new StreamInfoManageModel
            {
                RtmpUrl = i.RtmpUrl,
                RtmpKey = i.RtmpKey,
                Enabled = i.Enabled
            }).ToList();
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