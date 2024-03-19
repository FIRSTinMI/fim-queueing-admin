using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using fim_queueing_admin.Models;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

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
        public string? Credential { get; set; }
    }
    
    public async Task<JsonWebKeySet> FetchGoogleCertificates()
    {
        using var http = new HttpClient();
        var response = await http.GetAsync("https://www.googleapis.com/oauth2/v3/certs");

        return new JsonWebKeySet(await response.Content.ReadAsStringAsync());
    }

    private async Task<ClaimsPrincipal> ValidateToken(string idToken)
    {
        var certificates = await FetchGoogleCertificates();

        TokenValidationParameters tvp = new TokenValidationParameters()
        {
            ValidateActor = false, // check the profile ID

            ValidateAudience = true, // check the client ID
            ValidAudience = _configuration["Firebase:AuthClientId"],

            ValidateIssuer = true, // check token came from Google
            ValidIssuers = new List<string> { "accounts.google.com", "https://accounts.google.com" },

            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = certificates.GetSigningKeys(),
            ValidateLifetime = true,
            RequireExpirationTime = true
        };

        var jsth = new JwtSecurityTokenHandler();
        var cp = jsth.ValidateToken(idToken, tvp, out _);

        return cp;
    }

    [HttpPost]
    public async Task<IActionResult> ValidateToken([FromBody] LoginModel loginModel, [FromServices] FirebaseAuth auth)
    {
        if (loginModel.Credential is null) return Forbid();
        var principal = await ValidateToken(loginModel.Credential);
        var emailClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)!;

        var user = await auth.GetUserByEmailAsync(emailClaim.Value);
        var accessLevel = "";
        
        user.CustomClaims.TryGetValue(ClaimTypes.AccessLevel, out var accessLevelObj);
        if (accessLevelObj is not null) accessLevel = accessLevelObj.ToString()!;

        if (string.IsNullOrWhiteSpace(accessLevel))
        {
            return Forbid();
        }

        await _auth.SignInAsync(HttpContext, AuthScheme,
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        emailClaim,
                        new(ClaimTypes.AccessLevel, accessLevel)
                    }, "fim",
                    "name", "role")), new()
            {
                IssuedUtc = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirst("iat")!.Value)),
                ExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(long.Parse(principal.FindFirst("exp")!.Value))
            });
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
    
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult AccessDenied()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}