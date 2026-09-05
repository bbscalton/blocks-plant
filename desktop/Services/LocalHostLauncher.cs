using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace BlocksPlant.Desktop.Services;

/// <summary>
/// Starts local API / Blazor web hosts when the Desktop POS launches from a repo checkout.
/// Does not kill started processes on Desktop exit (other clients may still need them).
/// </summary>
public sealed class LocalHostLauncher
{
    public static LocalHostLauncher Instance { get; } = new();

    private static readonly HttpClient ProbeHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly List<int> _startedPids = new();
    private readonly object _lock = new();

    public IReadOnlyList<int> StartedProcessIds
    {
        get
        {
            lock (_lock)
                return _startedPids.ToArray();
        }
    }

    public string? LastError { get; private set; }

    public async Task EnsureAllAsync(AppConfig config, Action<string>? status = null, CancellationToken ct = default)
    {
        LastError = null;

        try
        {
            if (config.AutoStartApi)
            {
                status?.Invoke("Starting API…");
                await EnsureApiAsync(config, ct);
            }

            if (config.AutoStartWeb)
            {
                status?.Invoke("Starting web dashboard…");
                await EnsureWebAsync(config, ct);
            }

            status?.Invoke(string.IsNullOrEmpty(LastError) ? "Local hosts ready" : LastError);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            status?.Invoke($"Auto-start failed: {ex.Message}");
        }
    }

    public async Task EnsureApiAsync(AppConfig config, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiBaseUrl)
            ? "http://localhost:5118"
            : config.ApiBaseUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
            return;

        var projectDir = FindSiblingProjectDir("backend", "BlocksPlant.Api.csproj");
        if (projectDir is null)
        {
            LastError = "API is not running and the backend project was not found near this exe.";
            return;
        }

        StartDotnetHost(projectDir, "BlocksPlant.Api.csproj");
        var ok = await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct);
        if (!ok)
            LastError = $"API did not become ready at {baseUrl}. Start it manually from the backend folder.";
    }

    public async Task EnsureWebAsync(AppConfig config, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.WebUrl)
            ? "http://localhost:5137"
            : config.WebUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
            return;

        var projectDir = FindSiblingProjectDir("web", "BlocksPlant.Web.csproj");
        if (projectDir is null)
        {
            // Web is optional for POS login — soft failure
            if (string.IsNullOrEmpty(LastError))
                LastError = "Web dashboard is not running and the web project was not found.";
            return;
        }

        StartDotnetHost(projectDir, "BlocksPlant.Web.csproj");
        var ok = await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct);
        if (!ok && string.IsNullOrEmpty(LastError))
            LastError = $"Web did not become ready at {baseUrl}.";
    }

    public static bool IsPortOpen(string host, int port, int timeoutMs = 500)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            if (!task.Wait(timeoutMs))
                return false;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> HttpGetOkAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var response = await ProbeHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            // Any HTTP response means the host is listening (404/401 still count).
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsServiceUpAsync(string baseUrl, CancellationToken ct)
    {
        if (TryParseHostPort(baseUrl, out var host, out var port) && IsPortOpen(host, port))
            return true;

        return await HttpGetOkAsync(baseUrl + "/", ct);
    }

    private static async Task<bool> WaitUntilUpAsync(string baseUrl, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsServiceUpAsync(baseUrl, ct))
                return true;
            await Task.Delay(750, ct);
        }

        return false;
    }

    private void StartDotnetHost(string projectDir, string csprojName)
    {
        var csproj = Path.Combine(projectDir, csprojName);

        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --launch-profile http --project \"{csproj}\"",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            process = Process.Start(psi);
        }
        catch
        {
            // Fall through to built exe.
        }

        if (process is null)
        {
            var fallback = BuildExeStartInfo(projectDir, csprojName)
                           ?? throw new InvalidOperationException($"Failed to start process for {csprojName}.");
            process = Process.Start(fallback)
                      ?? throw new InvalidOperationException($"Failed to start process for {csprojName}.");
        }

        lock (_lock)
            _startedPids.Add(process.Id);
    }

    private static ProcessStartInfo? BuildExeStartInfo(string projectDir, string csprojName)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(csprojName);
        var candidates = new[]
        {
            Path.Combine(projectDir, "bin", "Debug", "net8.0", $"{assemblyName}.exe"),
            Path.Combine(projectDir, "bin", "Release", "net8.0", $"{assemblyName}.exe")
        };
        var builtExe = candidates.FirstOrDefault(File.Exists);
        if (builtExe is null)
            return null;

        var psi = new ProcessStartInfo
        {
            FileName = builtExe,
            WorkingDirectory = projectDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        if (csprojName.Contains("Api", StringComparison.OrdinalIgnoreCase))
            psi.Environment["ASPNETCORE_URLS"] = "http://localhost:5118";
        else
            psi.Environment["ASPNETCORE_URLS"] = "http://localhost:5137";
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        return psi;
    }

    /// <summary>
    /// Walk up from the Desktop exe directory until a sibling <paramref name="folderName"/>
    /// containing <paramref name="csprojName"/> is found (repo root layout).
    /// </summary>
    public static string? FindSiblingProjectDir(string folderName, string csprojName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, folderName);
            var csproj = Path.Combine(candidate, csprojName);
            if (Directory.Exists(candidate) && File.Exists(csproj))
                return candidate;

            // Also: if we are inside desktop/, look at parent for backend/web
            if (dir.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
            {
                candidate = Path.Combine(dir.Parent.FullName, folderName);
                csproj = Path.Combine(candidate, csprojName);
                if (Directory.Exists(candidate) && File.Exists(csproj))
                    return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool TryParseHostPort(string url, out string host, out int port)
    {
        host = "localhost";
        port = 80;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            host = string.IsNullOrEmpty(uri.Host) ? "localhost" : uri.Host;
            port = uri.Port;
            return port > 0;
        }
        catch
        {
            return false;
        }
    }
}
