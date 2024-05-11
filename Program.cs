using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using fim_queueing_admin;
using fim_queueing_admin.Auth;
using fim_queueing_admin.Data;
using fim_queueing_admin.Hubs;
using fim_queueing_admin.Services;
using Firebase.Database;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Diagnostics.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SlackNet;
using TwitchLib.Api;
using Action = fim_queueing_admin.Auth.Action;

var builder = WebApplication.CreateBuilder(args);

var accountCred = await GoogleCredential.GetApplicationDefaultAsync();
var credential = accountCred.CreateScoped(
    "https://www.googleapis.com/auth/userinfo.email",
    "https://www.googleapis.com/auth/firebase.database");

async Task<string> GetAccessToken()
{
    return await (credential as ITokenAccess).GetAccessTokenForRequestAsync();
}

FirebaseApp.Create(new AppOptions
{
    ProjectId = builder.Configuration["Firebase:ProjectId"] ?? "fim-queueing",
    Credential = credential
});

builder.Services.AddSingleton(_ => new FirebaseClient(builder.Configuration["Firebase:BaseUrl"],
    new FirebaseOptions
    {
        AuthTokenAsyncFactory = GetAccessToken,
        AsAccessToken = true
    }));

builder.Services.AddDbContext<FimDbContext>(opt =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    opt.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
});

builder.Services.AddSingleton<FirebaseAuth>(_ => FirebaseAuth.DefaultInstance);

builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opt =>
{
    opt.LoginPath = "/Home/Login";
    opt.SlidingExpiration = true;
    opt.ExpireTimeSpan = TimeSpan.FromDays(31);
    opt.AccessDeniedPath = "/Home/AccessDenied";
}).AddScheme<AuthTokenAuthSchemeOptions, AuthTokenAuthSchemeHandler>(AuthTokenScheme.AuthenticationScheme, _ => { });

builder.Services.AddAuthorization(opt =>
{
    opt.DefaultPolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().Build();

    var authTokenPolicy =
        new AuthorizationPolicyBuilder(AuthTokenScheme.AuthenticationScheme).RequireClaim(ClaimTypes.CartId).Build();
    opt.AddPolicy(AuthTokenScheme.AuthenticationScheme, authTokenPolicy);

    foreach (var action in typeof(Action).GetFields().Select(f => (string)f.GetValue(null)!))
    {
        opt.AddPolicy($"Action:{action}",
            new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddRequirements(new UserAccessRequirement(action)).Build());
    }
});

builder.Services.AddSingleton<IAuthorizationHandler, UserAccessHandler>();
builder.Services.AddSingleton<DisplayHubManager>();
builder.Services.AddSignalR();

builder.Services.AddCors(opt => opt.AddDefaultPolicy(pol =>
{
    pol.AllowAnyHeader();
    pol.AllowAnyMethod();
    pol.AllowCredentials();
    pol.SetIsOriginAllowed(_ => true);
}));

var services = Assembly.GetExecutingAssembly().GetTypes()
    .Where(mytype => mytype.GetInterfaces().Contains(typeof(IService)));

foreach (var service in services) builder.Services.AddScoped(service);

if (string.IsNullOrWhiteSpace(builder.Configuration["FRCAPIToken"]))
    throw new ApplicationException("FRC API Token is required to start up");
builder.Services.AddHttpClient("FRC", client =>
{
    client.BaseAddress = new Uri("https://frc-api.firstinspires.org/v3.0/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(builder.Configuration["FRCAPIToken"]!)));
});
if (string.IsNullOrWhiteSpace(builder.Configuration["TBAAPIToken"]))
    throw new ApplicationException("TBA API Token is required to start up");
builder.Services.AddHttpClient("TBA", client =>
{
    client.BaseAddress = new Uri("https://www.thebluealliance.com/api/v3/");
    client.DefaultRequestHeaders.Add("X-TBA-Auth-Key", builder.Configuration["TBAAPIToken"]);
});

if (!string.IsNullOrEmpty(builder.Configuration["Twitch:ClientId"]) &&
    !string.IsNullOrEmpty(builder.Configuration["Twitch:ClientSecret"]))
{
    builder.Services.AddSingleton(_ => new TwitchAPI
    {
        Settings =
        {
            ClientId = builder.Configuration["Twitch:ClientId"],
            Secret = builder.Configuration["Twitch:ClientSecret"]
        }
    });
}

if (!string.IsNullOrEmpty(builder.Configuration["Slack:Token"]))
{
    builder.Services.AddSingleton(_ =>
        new SlackServiceBuilder().UseApiToken(builder.Configuration["Slack:Token"]).GetApiClient());
}

// Some stuff will hardly ever change, so just fetch it once at startup.
// If I cared more this might be an expiring cache
builder.Services.AddSingleton<GlobalState>(provider =>
{
    var season = provider.GetRequiredService<FirebaseClient>().Child("/current_season")
        .OnceSingleAsync<string>();
    season.Wait();
    using var versionStream = Assembly.GetEntryAssembly()?
        .GetManifestResourceStream("fim_queueing_admin.Assets.Version.txt");
    if (versionStream == null) throw new NullReferenceException("Version info was null");
    using var version = new StreamReader(versionStream);
    return new GlobalState(season.Result, version.ReadToEnd());
});

if (!string.IsNullOrEmpty(builder.Configuration["Logging:GoogleProjectId"]))
{
    builder.Logging.AddGoogle(new LoggingServiceOptions()
    {
        ProjectId = builder.Configuration["Logging:GoogleProjectId"],
        ServiceName = "fim-queueing-admin"
    });
}

var isBehindProxy = bool.TryParse(builder.Configuration["EnableForwardedHeaders"], out var res) && res;

if (isBehindProxy)
{
    var proxyIpAddress = builder.Configuration["ProxyIPAddress"];
    if (proxyIpAddress is null)
        throw new ApplicationException("Forwarded headers were enabled but no proxy IP was defined");
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(IPAddress.Parse(proxyIpAddress));
    });
}

builder.Services.AddHostedService<DatabaseKeepAliveService>();

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("assistant", pol =>
    {
        pol.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials().Build();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseCors();

if (bool.TryParse(app.Configuration["EnableForwardedHeaders"], out var proxy) && proxy)
    app.UseForwardedHeaders();
else
    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors();

app.MapHub<DisplayHub>("/DisplayHub");
app.MapHub<AssistantHub>("/AssistantHub").RequireCors("assistant");
app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.Run();