using fim_queueing_admin.Auth;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Mvc;
using Action = fim_queueing_admin.Auth.Action;

namespace fim_queueing_admin.Controllers;

[AuthorizeOperation(Action.ViewUser)]
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

    [AuthorizeOperation(Action.ManageUser)]
    [HttpGet("[action]/{uid}")]
    public ActionResult Manage(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return BadRequest();
        ViewData["uid"] = uid;
        return View();
    }
    
    [AuthorizeOperation(Action.ManageUser)]
    [HttpPost("[action]/{uid}")]
    public async Task<ActionResult> Manage(string uid, [FromForm] UserManageModel model)
    {
        if (string.IsNullOrEmpty(uid)) return BadRequest();

        var user = await _auth.GetUserAsync(uid);
        var claims = user.CustomClaims.ToDictionary(p => p.Key, p => p.Value);
        claims["admin"] = model.IsAdmin;
        if (!string.IsNullOrEmpty(model.UserAccessLevel))
        {
            claims[ClaimTypes.AccessLevel] = model.UserAccessLevel;
        }
        else
        {
            claims.Remove(ClaimTypes.AccessLevel);
        }

        await _auth.SetCustomUserClaimsAsync(user.Uid, claims);

        return RedirectToAction(nameof(Index));
    }

    public class UserManageModel
    {
        public bool IsAdmin { get; set; }
        public string? UserAccessLevel { get; set; }
    }
}