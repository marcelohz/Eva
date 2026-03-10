using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Services;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// --- SECURITY CHECK ---
// Ensure secrets exist in secrets.json. If they don't, the app won't even start.
var turnstileSecret = builder.Configuration["Turnstile:SecretKey"];
if (string.IsNullOrEmpty(turnstileSecret))
{
    throw new Exception("CRITICAL ERROR: 'Turnstile:SecretKey' is missing from secrets.json.");
}

// --- DATABASE & CONTEXT ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<EvaDbContext>(options => options.UseNpgsql(connectionString));

// --- EXISTING SERVICES ---
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PendenciaService>();
builder.Services.AddScoped<ArquivoService>();

// --- NEW SERVICES ---
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<TurnstileSettings>(builder.Configuration.GetSection("Turnstile"));
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();
builder.Services.AddTransient<IEmailService, EmailService>(); // You'll need this for Hangfire

// THE FIX: Register the EntityStatusService so NovaViagem (and the rest of the app) can use it!
builder.Services.AddScoped<IEntityStatusService, EntityStatusService>();

// --- HANGFIRE (Dedicated Schema) ---
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true
    }));
builder.Services.AddHangfireServer(options => { options.WorkerCount = 1; });

// --- AUTHENTICATION ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Error";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });

// --- LOCALIZATION ---
var supportedCultures = new[] { "pt-BR" };
builder.Services.Configure<RequestLocalizationOptions>(options => {
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("pt-BR");
    options.SupportedCultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList();
    options.SupportedUICultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList();
});

var app = builder.Build();

app.UseRequestLocalization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");

app.MapRazorPages();
app.Run();