using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MomentumGroves.Data;
using MomentumGroves.Models;
using MomentumGroves.Service;

namespace MomentumGroves.PlaywrightTests;

internal sealed class SqliteWebHost : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("momentum-playwright-");
    private Process? _process;
    private bool _started;
    public string BaseUrl { get; private set; } = "";

    /// <summary>
    /// Starts an isolated SQLite-backed instance of MomentumGroves for Playwright.
    /// </summary>
    /// <returns>The running host with its loopback URL ready for browser requests.</returns>
    public static async Task<SqliteWebHost> StartAsync()
    {
        var host = new SqliteWebHost();
        try
        {
            await host.StartProcessAsync();
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates temporary SQLite databases, seeds an ingredient, and waits for the web app to report its listening URL.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException">The repository's wwwroot directory could not be found.</exception>
    private async Task StartProcessAsync()
    {
        using var database = new PersonalDatabase(Path.Combine(_directory.FullName, "src", "Data"));
        _ = new MomentumDataService(database, NullLogger<MomentumDataService>.Instance);
        var library = new FoodLibraryService(database, NullLogger<FoodLibraryService>.Instance);
        library.SaveFood(null, new SaveLibraryFood("Ekstra Jomfru Olivenolie", "g", 884, 0, 0, 100, null));
        SqliteConnection.ClearAllPools();

        var webRoot = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (!Directory.Exists(Path.Combine(webRoot.FullName, "wwwroot")))
            webRoot = webRoot.Parent ?? throw new DirectoryNotFoundException("MomentumGroves wwwroot was not found.");

        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var dotnet = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", executable));
        var startup = new ProcessStartInfo(dotnet)
        {
            WorkingDirectory = _directory.FullName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var variable in startup.Environment.Keys.Where(key =>
                     key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("Kestrel__", StringComparison.OrdinalIgnoreCase) ||
                     key.StartsWith("ConnectionStrings__", StringComparison.OrdinalIgnoreCase)).ToArray())
            startup.Environment.Remove(variable);

        foreach (var argument in new[]
                 {
                     typeof(MomentumDataService).Assembly.Location,
                     "--environment", "Testing", "--contentRoot", _directory.FullName,
                     "--webroot", Path.Combine(webRoot.FullName, "wwwroot"),
                     "--urls", "http://127.0.0.1:0", "--ConnectionStrings:Supabase=",
                     "--MobileSync:Port=0", "--MobileSync:Key=",
                     "--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information"
                 })
            startup.ArgumentList.Add(argument);

        var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _process = new Process { StartInfo = startup, EnableRaisingEvents = true };
        _process.Exited += (_, _) =>
            ready.TrySetException(new InvalidOperationException("The Playwright SQLite web host exited during startup."));
        _process.OutputDataReceived += (_, eventArgs) =>
        {
            const string addressPrefix = "Now listening on: ";
            var line = eventArgs.Data?.Trim();
            if (line?.StartsWith(addressPrefix, StringComparison.Ordinal) == true)
                ready.TrySetResult(line[addressPrefix.Length..]);
        };
        _process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is { } line)
                TestContext.Progress.WriteLine(line);
        };

        _process.Start();
        _started = true;
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        BaseUrl = await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Stops the web app process and removes the temporary test directory.
    /// </summary>
    public void Dispose()
    {
        if (_process is not null)
        {
            if (_started)
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
                _process.WaitForExit();
            }
            _process.Dispose();
        }

        _directory.Delete(recursive: true);
    }
}
