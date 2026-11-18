MomentumGroves.Helper.DotEnvLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MomentumGroves.Service.DatabaseInitializationService>();
builder.Services.AddScoped<MomentumGroves.Service.DrinkService>();
builder.Services.AddScoped<MomentumGroves.Service.FoodService>();
builder.Services.AddScoped<MomentumGroves.Service.MobileFoodSyncService>();
builder.Services.AddScoped<MomentumGroves.Repository.MobileFoodSyncRepository>();
builder.Services.AddSingleton<MomentumGroves.Service.FoodLibraryService>();
builder.Services.AddSingleton<MomentumGroves.Service.MomentumDataService>();
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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

if (!app.Environment.IsDevelopment())
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
