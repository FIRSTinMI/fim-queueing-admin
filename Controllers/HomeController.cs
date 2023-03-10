using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using fim_queueing_admin.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace fim_queueing_admin.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationService _auth;
    
    private const string AuthScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IAuthenticationService auth)
    {
        _logger = logger;
        _configuration = configuration;
        _auth = auth;
    }

    [Authorize]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Display");
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    public class LoginModel
    {
        [PasswordPropertyText]
        public string? Password { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginModel loginModel)
    {
        if (string.IsNullOrWhiteSpace(loginModel.Password) || loginModel.Password != _configuration["Password"])
        {
            _logger.LogWarning("Failed login from user at {IP}", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError("password", "Incorrect password");
            return View();
        }

        await _auth.SignInAsync(HttpContext, AuthScheme,
            new ClaimsPrincipal(
                new ClaimsIdentity(new Claim[] { new("name", Guid.NewGuid().ToString()) }, "fim",
                "name", "role")), null);
        _logger.LogInformation("Successful login from user at {IP}", HttpContext.Connection.RemoteIpAddress);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _auth.SignOutAsync(HttpContext, AuthScheme, null);

        return RedirectToAction(nameof(Login));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}