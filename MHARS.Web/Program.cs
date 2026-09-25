using DotNetEnv;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MHARS.Web.Data;
using MHARS.Web.Models.Agent;
using MHARS.Web.Services;

// Load .env if it exists (local dev only). The hosting server has none - that is expected.
foreach (var candidate in new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env")
})
{
    if (File.Exists(candidate))
    {
        Env.Load(candidate);
        break;
    }
}

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
//  Database
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------------------------------------------------------------------------
//  Identity
// ---------------------------------------------------------------------------
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ---------------------------------------------------------------------------
//  MVC + Razor Pages
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------------------------------------------------------------------------
//  USGS earthquake ingestion
// ---------------------------------------------------------------------------
builder.Services.Configure<UsgsOptions>(builder.Configuration.GetSection("Usgs"));

builder.Services.AddHttpClient("usgs", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<UsgsOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
});

builder.Services.AddSingleton<UsgsSyncStatus>();
builder.Services.AddScoped<UsgsEarthquakeService>();
builder.Services.AddHostedService<UsgsSyncBackgroundService>();

// ---------------------------------------------------------------------------
//  Groq AI agent
// ---------------------------------------------------------------------------
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection("Groq"));
builder.Services.AddHttpClient<IGroqAgentService, GroqAgentService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
//  Apply migrations, then seed.
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<ApplicationDbContext>();

    logger.LogInformation("EF provider  : {Provider}", db.Database.ProviderName);
    logger.LogInformation("Database     : {Database}", db.Database.GetDbConnection().Database);

    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Migrations applied.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "MIGRATION FAILED.");
        throw;
    }

    try
    {
        await DbSeeder.SeedAsync(services);
        logger.LogInformation("Seeding complete.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SEEDING FAILED - site will still run but data may be empty.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();