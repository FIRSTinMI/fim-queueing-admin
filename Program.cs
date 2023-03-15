using fim_queueing_admin;
using fim_queueing_admin.Hubs;
using Firebase.Database;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using fim_queueing_admin.Services;

var builder = WebApplication.CreateBuilder(args);

async Task<string> GetAccessToken()
{
    var accountCred = await GoogleCredential.GetApplicationDefaultAsync();
    var credential = accountCred.CreateScoped(
        "https://www.googleapis.com/auth/userinfo.email",
        "https://www.googleapis.com/auth/firebase.database");
    
    return await (credential as ITokenAccess).GetAccessTokenForRequestAsync();
}

builder.Services.AddSingleton(_ => new FirebaseClient(builder.Configuration["Firebase:BaseUrl"],
    new FirebaseOptions()
    {
        AuthTokenAsyncFactory = GetAccessToken,
        AsAccessToken = true
    }));

builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(opt =>
{
    opt.LoginPath = "/Home/Login";
    opt.SlidingExpiration = true;
    opt.ExpireTimeSpan = TimeSpan.FromDays(31);
});
builder.Services.AddAuthorization(opt =>
{
    opt.DefaultPolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser().Build();
});

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

foreach (var service in services)
{
    builder.Services.AddScoped(service);
}

if (string.IsNullOrWhiteSpace(builder.Configuration["FRCAPIToken"]))
{
    throw new ApplicationException("FRC API Token is required to start up");
}
builder.Services.AddHttpClient("FRC", client =>
{
    client.BaseAddress = new Uri("https://frc-api.firstinspires.org/v3.0/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(builder.Configuration["FRCAPIToken"])));
});

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

if (bool.TryParse(builder.Configuration["EnableForwardedHeaders"], out var builderProxy) && builderProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(IPAddress.Parse(builder.Configuration["ProxyIPAddress"]));
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (bool.TryParse(app.Configuration["EnableForwardedHeaders"], out var proxy) && proxy)
{
    app.UseForwardedHeaders();
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors();

app.MapHub<DisplayHub>("/DisplayHub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
