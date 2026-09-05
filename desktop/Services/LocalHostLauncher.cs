using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace BlocksPlant.Desktop.Services;

/// <summary>
/// Starts local API / Blazor web hosts when the Desktop POS launches.
/// Prefers published sibling exes (installed layout under Program Files);
/// falls back to <c>dotnet run</c> only when developing from the repo.
/// Does not kill started processes on Desktop exit (other clients may still need them).
/// </summary>
public sealed class LocalHostLauncher
{
    public static LocalHostLauncher Instance { get; } = new();

    private const string ApiExeName = "BlocksPlant.Api.exe";
    private const string WebExeName = "BlocksPlant.Web.exe";
    private const string DefaultApiUrl = "http://localhost:5118";
    private const string DefaultWebUrl = "http://localhost:5137";

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
            ? DefaultApiUrl
            : config.ApiBaseUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
            return;

        if (TryStartPublishedExe("backend", ApiExeName, baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct))
                LastError = $"API did not become ready at {baseUrl}. Start BlocksPlant.Api manually from the backend folder.";
            return;
        }

        var projectDir = FindSiblingProjectDir("backend", "BlocksPlant.Api.csproj");
        if (projectDir is null)
        {
            LastError = "API is not running and neither a published backend exe nor the backend project was found near this app.";
            return;
        }

        StartDotnetHost(projectDir, "BlocksPlant.Api.csproj", DefaultApiUrl);
        if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct))
            LastError = $"API did not become ready at {baseUrl}. Start it manually from the backend folder.";
    }

    public async Task EnsureWebAsync(AppConfig config, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.WebUrl)
            ? DefaultWebUrl
            : config.WebUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
            return;

        if (TryStartPublishedExe("web", WebExeName, baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct) && string.IsNullOrEmpty(LastError))
                LastError = $"Web did not become ready at {baseUrl}.";
            return;
        }

        var projectDir = FindSiblingProjectDir("web", "BlocksPlant.Web.csproj");
        if (projectDir is null)
        {
            // Web is optional for POS login — soft failure
            if (string.IsNullOrEmpty(LastError))
                LastError = "Web dashboard is not running and neither a published web exe nor the web project was found.";
            return;
        }

        StartDotnetHost(projectDir, "BlocksPlant.Web.csproj", DefaultWebUrl);
        if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct) && string.IsNullOrEmpty(LastError))
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

    /// <summary>
    /// Start a self-contained published exe under sibling <paramref name="folderName"/>
    /// (installed layout: Program Files\BlocksPlant\Desktop\ + ..\backend\ + ..\web\).
    /// </summary>
    private bool TryStartPublishedExe(string folderName, string exeName, string urls)
    {
        var exePath = FindSiblingPublishedExe(folderName, exeName);
        if (exePath is null)
            return false;

        var workDir = Path.GetDirectoryName(exePath)!;
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--urls {urls}",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        psi.Environment["ASPNETCORE_URLS"] = urls;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";

        var process = Process.Start(psi);
        if (process is null)
            return false;

        lock (_lock)
            _startedPids.Add(process.Id);
        return true;
    }

    private void StartDotnetHost(string projectDir, string csprojName, string urls)
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
            var fallback = BuildDevExeStartInfo(projectDir, csprojName, urls)
                           ?? throw new InvalidOperationException($"Failed to start process for {csprojName}.");
            process = Process.Start(fallback)
                      ?? throw new InvalidOperationException($"Failed to start process for {csprojName}.");
        }

        lock (_lock)
            _startedPids.Add(process.Id);
    }

    private static ProcessStartInfo? BuildDevExeStartInfo(string projectDir, string csprojName, string urls)
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
        psi.Environment["ASPNETCORE_URLS"] = urls;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        return psi;
    }

    /// <summary>
    /// Resolve published sibling exe: ..\{folder}\{exe} from Desktop base dir,
    /// or walk up looking for an install/repo root that contains the folder.
    /// </summary>
    public static string? FindSiblingPublishedExe(string folderName, string exeName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            // Direct sibling: ...\Desktop\ -> ...\backend\BlocksPlant.Api.exe
            if (dir.Parent is not null)
            {
                var sibling = Path.Combine(dir.Parent.FullName, folderName, exeName);
                if (File.Exists(sibling))
                    return sibling;
            }

            // Install / publish root containing Desktop + backend + web
            var underRoot = Path.Combine(dir.FullName, folderName, exeName);
            if (File.Exists(underRoot))
                return underRoot;

            // Repo-style: walking from ...\desktop\bin\... up to repo root
            if (dir.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
            {
                var candidate = Path.Combine(dir.Parent.FullName, folderName, exeName);
                if (File.Exists(candidate))
                    return candidate;
            }

            dir = dir.Parent;
        }

        return null;
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
