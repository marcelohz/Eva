using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Services;
using Eva.Configuration; // <-- NEW: Centralized Configuration Namespace
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using Serilog.Events;

// 1. INITIAL LOGGING SETUP (Bootstrap Logger)
// This catches setup errors before the DI container and Configuration are fully built.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Eva Project web host...");

    var builder = WebApplication.CreateBuilder(args);

    // Tell the host to use Serilog and read configuration from the current appsettings.*.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // --- STRICT OPTIONS VALIDATION ---
    // Replaces old Configure calls and manual checks.
    // Automatically throws an OptionsValidationException on startup if any required variable is missing or invalid.

    builder.Services.AddOptions<DatabaseSettings>()
        .BindConfiguration("ConnectionStrings")
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<TurnstileSettings>()
        .BindConfiguration("Turnstile")
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<EmailSettings>()
        .BindConfiguration("EmailSettings")
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // --- DATABASE & CONTEXT ---
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<EvaDbContext>(options => options.UseNpgsql(connectionString));

    // --- INFRASTRUCTURE SERVICES ---
    builder.Services.AddRazorPages();
    builder.Services.AddHttpContextAccessor();

    // Core Domain Services
    builder.Services.AddScoped<PendenciaService>();
    builder.Services.AddScoped<ArquivoService>();
    builder.Services.AddScoped<IEntityStatusService, EntityStatusService>();

    // External Integrations (Email & Turnstile)
    builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();
    builder.Services.AddTransient<IEmailService, EmailService>();

    // --- HANGFIRE (Background Jobs) ---
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
            options.Cookie.Name = "Eva_Auth_Session"; // Explicit name to avoid conflicts on the same server
        });

    // --- LOCALIZATION (pt-BR) ---
    var supportedCultures = new[] { "pt-BR" };
    builder.Services.Configure<RequestLocalizationOptions>(options => {
        options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("pt-BR");
        options.SupportedCultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList();
        options.SupportedUICultures = supportedCultures.Select(c => new System.Globalization.CultureInfo(c)).ToList();
    });

    // --- AUTHORIZATION POLICIES ---
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AcessoEmpresa", policy =>
            policy.RequireRole("EMPRESA", "USUARIO_EMPRESA"));
    });

    var app = builder.Build();

    // --- MIDDLEWARE PIPELINE ---

    app.UseRequestLocalization();

    if (!app.Environment.IsDevelopment())
    {
        // Production and Staging: Users see the Error page, while Serilog writes details to the log file.
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // Captures HTTP request metadata in the logs (IPs, Paths, Status Codes)
    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseHangfireDashboard("/hangfire");

    app.MapRazorPages();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}