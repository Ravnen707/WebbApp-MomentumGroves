using MomentumGroves.Data;
using MomentumGroves.Repository;
using MomentumGroves.Service;

MomentumGroves.Helper.DotEnvLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
var migrate = args.FirstOrDefault() == "migrate-sqlite";
var builder = WebApplication.CreateBuilder(migrate ? [] : args);
if (migrate)
{
    Environment.ExitCode = SqliteDataMigration.Run(args[1..], builder.Configuration);
    return;
}

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(services => new PersonalDatabase(
    Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "src", "Data"),
    services.GetRequiredService<IConfiguration>().GetConnectionString("Supabase")));
builder.Services.AddSingleton<DatabaseInitializationService>();
builder.Services.AddScoped<MomentumGroves.Service.DrinkService>();
builder.Services.AddScoped<MomentumGroves.Service.FoodService>();
builder.Services.AddScoped<MomentumGroves.Service.MobileFoodSyncService>();
builder.Services.AddScoped(services => new MobileFoodSyncRepository(services.GetRequiredService<PersonalDatabase>()));
builder.Services.AddSingleton(services => new FoodLibraryService(services.GetRequiredService<PersonalDatabase>(),
    services.GetRequiredService<ILogger<FoodLibraryService>>()));
builder.Services.AddSingleton(services => new MomentumDataService(services.GetRequiredService<PersonalDatabase>(),
    services.GetRequiredService<ILogger<MomentumDataService>>()));
builder.Services.AddSingleton<MomentumGroves.Service.TrainingImageService>();
builder.Services.AddHttpClient<MomentumGroves.Service.FridaUpdateService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MomentumGroves/1.0 (Frida dataset administration)");
});
builder.Services.AddHttpClient("openfoodfacts", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "MomentumGroves/0.1 (personal nutrition diary; contact: local development)");
});
builder.Services.AddHttpClient();
var app = builder.Build();
// Capture file existence before any other application service can create a database.
_ = app.Services.GetRequiredService<MomentumGroves.Service.DatabaseInitializationService>();
app.Services.GetRequiredService<PersonalDatabase>().InitializePostgres();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

var mobileSyncPort = app.Configuration.GetValue<int?>("MobileSync:Port");
if (mobileSyncPort is int port)
{
    app.Use(async (context, next) =>
    {
        if (context.Connection.LocalPort == port && context.Connection.LocalIpAddress is { } ip &&
            !System.Net.IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))
        {
            var path = context.Request.Path;
            var method = context.Request.Method;
            var syncRequest =
                (HttpMethods.IsPost(method) && path == "/api/mobile/foods/import") ||
                ((HttpMethods.IsPost(method) || HttpMethods.IsGet(method)) && path == "/api/mobile/training/sync") ||
                (HttpMethods.IsGet(method) && path.StartsWithSegments("/TrainingImages"));
            if (!context.Request.IsHttps || !syncRequest)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await next();
    });
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:5000",
            UseShellExecute = true
        });
    });
}

app.Run();
