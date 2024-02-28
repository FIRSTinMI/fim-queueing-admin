using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using fim_queueing_admin.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace fim_queueing_admin.Auth;

public static class AuthTokenScheme
{
    public const string AuthenticationScheme = "AuthToken";
} 

public class AuthTokenAuthSchemeHandler(
    IOptionsMonitor<AuthTokenAuthSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FimDbContext dbContext)
    : AuthenticationHandler<AuthTokenAuthSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Read the token from request headers/cookies
        // Check that it's a valid session, depending on your implementation
        var token = Request.Query["access_token"];
        if (string.IsNullOrEmpty(token))
        {
            if (AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var headerToken))
            {
                token = headerToken.Parameter;
            }
        }

        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Failed to retrieve token");
        }

        var cart = await dbContext.Carts.SingleOrDefaultAsync(c => c.AuthToken == token.ToString());

        if (cart is null)
        {
            return AuthenticateResult.Fail("Incorrect API key");
        }

        // If the session is valid, return success:
        var claims = new[]
        {
            new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, cart.Id.ToString()),
            new Claim(ClaimTypes.CartId, cart.Id.ToString()),
            new Claim(ClaimTypes.CartName, cart.Name)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthTokenScheme.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}