using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;   // needed for IOptions<T>; not in the implicit usings
using MHARS.Web.Data;
using MHARS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
    {
        // Retry on transient network / LocalDB startup failures.
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------------------------------------------------------------------------
//  USGS earthquake ingestion
//
//  A named HttpClient, not AddHttpClient<TService>. The typed-client overload
//  registers the service as TRANSIENT, and this service holds a scoped
//  DbContext — that combination works when resolved inside a request scope and
//  throws when resolved from the root provider. A named client plus an explicit
//  AddScoped removes the trap entirely.
// ---------------------------------------------------------------------------
builder.Services.Configure<UsgsOptions>(builder.Configuration.GetSection("Usgs"));

builder.Services.AddHttpClient("usgs", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<UsgsOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(opts.UserAgent);
});

builder.Services.AddSingleton<UsgsSyncStatus>();      // singleton: survives across scopes
builder.Services.AddScoped<UsgsEarthquakeService>();  // scoped: it holds a DbContext
builder.Services.AddHostedService<UsgsSyncBackgroundService>();

var app = builder.Build();

// Apply migrations, then seed. Logged so startup failures are visible.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("EF provider  : {Provider}", db.Database.ProviderName);
        logger.LogInformation("Database     : {Database}", db.Database.GetDbConnection().Database);

        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(services);

        logger.LogInformation("Database ready and seeded.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DATABASE MIGRATION OR SEEDING FAILED.");
        throw;
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