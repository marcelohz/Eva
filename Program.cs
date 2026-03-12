using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Eva.Data;
using Eva.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Serilog;
using Serilog.Events;

// 1. INITIAL LOGGING SETUP
// This is configured before the builder to catch any startup or configuration errors.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Filters out verbose system logs
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("/var/log/eva/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7) // Keeps 7 days of logs
    .CreateLogger();

try
{
    Log.Information("Starting Eva Project web host...");

    var builder = WebApplication.CreateBuilder(args);

    // Tell the host to use Serilog for all internal logging
    builder.Host.UseSerilog();

    // --- SECURITY CHECK ---
    // Critical validation: refuses to start if security keys are missing from configuration.
    var turnstileSecret = builder.Configuration["Turnstile:SecretKey"];
    if (string.IsNullOrEmpty(turnstileSecret))
    {
        Log.Fatal("CRITICAL ERROR: 'Turnstile:SecretKey' is missing from configuration.");
        throw new Exception("CRITICAL ERROR: 'Turnstile:SecretKey' is missing from configuration.");
    }

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
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
    builder.Services.Configure<TurnstileSettings>(builder.Configuration.GetSection("Turnstile"));
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
        // Production: Users see the Error page, while Serilog writes details to the log file.
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