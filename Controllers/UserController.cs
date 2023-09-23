using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fim_queueing_admin.Controllers;

[Authorize]
[Route("[controller]")]
public class UserController : Controller
{
    private readonly FirebaseAuth _auth;

    public UserController(FirebaseAuth auth)
    {
        _auth = auth;
    }

    [HttpGet]
    public ActionResult Index()
    {
        return View();
    }

    [HttpGet("[action]/{uid}")]
    public ActionResult Manage(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return BadRequest();
        ViewData["uid"] = uid;
        return View();
    }
    
    [HttpPost("[action]/{uid}")]
    public async Task<ActionResult> Manage(string uid, [FromForm] UserManageModel model)
    {
        if (string.IsNullOrEmpty(uid)) return BadRequest();

        var user = await _auth.GetUserAsync(uid);
        var claims = user.CustomClaims.ToDictionary(p => p.Key, p => p.Value);
        claims["admin"] = model.IsAdmin;

        await _auth.SetCustomUserClaimsAsync(user.Uid, claims);

        return RedirectToAction(nameof(Index));
    }

    public class UserManageModel
    {
        public bool IsAdmin { get; set; }
    }
}