using fim_queueing_admin.Hubs;
using Firebase.Database;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

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
