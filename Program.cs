using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Eva.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// ADD THIS: Allows EvaDbContext to read the current user's session (Claims/Cookies)
builder.Services.AddHttpContextAccessor();

// 1. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.Name = "Eva_Auth";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddDbContext<EvaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 2. Add these two in this specific order
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();