using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace BlocksPlant.Desktop.Services;

/// <summary>
/// Starts local API / Blazor web hosts when the Desktop POS launches.
/// Prefers published sibling exes (installed layout under Program Files / dist\publish);
/// falls back to built Debug/Release exes, then <c>dotnet run</c> when developing from the repo.
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
    private readonly string _logPath;

    public LocalHostLauncher()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BlocksPlant");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "host-launcher.log");
    }

    public IReadOnlyList<int> StartedProcessIds
    {
        get
        {
            lock (_lock)
                return _startedPids.ToArray();
        }
    }

    public string? LastError { get; private set; }

    /// <summary>Most recent status line (also written to the log).</summary>
    public string? LastStatus { get; private set; }

    public string LogPath => _logPath;

    public async Task EnsureAllAsync(AppConfig config, Action<string>? status = null, CancellationToken ct = default)
    {
        LastError = null;
        LastStatus = null;

        void Report(string message)
        {
            LastStatus = message;
            Log(message);
            status?.Invoke(message);
        }

        try
        {
            Log($"EnsureAll starting. BaseDirectory={AppContext.BaseDirectory}; ProcessPath={Environment.ProcessPath}");

            if (config.AutoStartApi)
            {
                Report("Starting API…");
                await EnsureApiAsync(config, ct);
            }

            if (config.AutoStartWeb)
            {
                Report("Starting web dashboard…");
                await EnsureWebAsync(config, ct);
            }

            Report(string.IsNullOrEmpty(LastError) ? "Local hosts ready" : LastError);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Report($"Auto-start failed: {ex.Message}");
            Log($"EXCEPTION: {ex}");
        }
    }

    public async Task EnsureApiAsync(AppConfig config, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.ApiBaseUrl)
            ? DefaultApiUrl
            : config.ApiBaseUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
        {
            Log($"API already up at {baseUrl}");
            return;
        }

        if (TryStartPublishedExe("backend", ApiExeName, baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct))
                SetError($"API did not become ready at {baseUrl}. See {LogPath}");
            return;
        }

        if (TryStartDevBuiltExe("backend", "BlocksPlant.Api.csproj", baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct))
                SetError($"API did not become ready at {baseUrl}. See {LogPath}");
            return;
        }

        var projectDir = FindSiblingProjectDir("backend", "BlocksPlant.Api.csproj");
        if (projectDir is null)
        {
            SetError("API is not running and neither a published backend exe nor the backend project was found near this app.");
            return;
        }

        if (!TryStartDotnetHost(projectDir, "BlocksPlant.Api.csproj", baseUrl))
        {
            SetError($"Failed to start API host from {projectDir}. See {LogPath}");
            return;
        }

        if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct))
            SetError($"API did not become ready at {baseUrl}. Start it manually from the backend folder. See {LogPath}");
    }

    public async Task EnsureWebAsync(AppConfig config, CancellationToken ct = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.WebUrl)
            ? DefaultWebUrl
            : config.WebUrl.TrimEnd('/');

        if (await IsServiceUpAsync(baseUrl, ct))
        {
            Log($"Web already up at {baseUrl}");
            return;
        }

        if (TryStartPublishedExe("web", WebExeName, baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct) && string.IsNullOrEmpty(LastError))
                SetError($"Web did not become ready at {baseUrl}. See {LogPath}");
            return;
        }

        if (TryStartDevBuiltExe("web", "BlocksPlant.Web.csproj", baseUrl))
        {
            if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct) && string.IsNullOrEmpty(LastError))
                SetError($"Web did not become ready at {baseUrl}. See {LogPath}");
            return;
        }

        var projectDir = FindSiblingProjectDir("web", "BlocksPlant.Web.csproj");
        if (projectDir is null)
        {
            if (string.IsNullOrEmpty(LastError))
                SetError("Web dashboard is not running and neither a published web exe nor the web project was found.");
            return;
        }

        if (!TryStartDotnetHost(projectDir, "BlocksPlant.Web.csproj", baseUrl))
        {
            if (string.IsNullOrEmpty(LastError))
                SetError($"Failed to start web host from {projectDir}. See {LogPath}");
            return;
        }

        if (!await WaitUntilUpAsync(baseUrl, TimeSpan.FromSeconds(90), ct) && string.IsNullOrEmpty(LastError))
            SetError($"Web did not become ready at {baseUrl}. See {LogPath}");
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
        if (TryParseHostPort(baseUrl, out var host, out var port))
        {
            // Trust TCP: if the port is closed, skip the 2s HTTP timeout.
            if (IsPortOpen(host, port))
                return true;
            return false;
        }

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
        {
            Log($"Published exe not found for {folderName}/{exeName}");
            return false;
        }

        Log($"Starting published exe: {exePath} --urls {urls}");
        return StartProcess(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--urls {urls}",
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }, urls, environment: "Production");
    }

    /// <summary>
    /// Prefer already-built Debug/Release host exes over a cold <c>dotnet run</c>.
    /// </summary>
    private bool TryStartDevBuiltExe(string folderName, string csprojName, string urls)
    {
        var projectDir = FindSiblingProjectDir(folderName, csprojName);
        if (projectDir is null)
            return false;

        var psi = BuildDevExeStartInfo(projectDir, csprojName, urls);
        if (psi is null)
        {
            Log($"No built Debug/Release exe under {projectDir}");
            return false;
        }

        Log($"Starting built dev exe: {psi.FileName}");
        return StartProcess(psi, urls, environment: "Development");
    }

    private bool TryStartDotnetHost(string projectDir, string csprojName, string urls)
    {
        var csproj = Path.Combine(projectDir, csprojName);
        Log($"Starting dotnet run for {csproj}");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --launch-profile http --project \"{csproj}\" -- --urls {urls}",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["ASPNETCORE_URLS"] = urls;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

            if (StartProcess(psi, urls, environment: null))
                return true;
        }
        catch (Exception ex)
        {
            Log($"dotnet run --no-build failed: {ex.Message}");
        }

        // Cold build path (first clone / clean bin)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --launch-profile http --project \"{csproj}\" -- --urls {urls}",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.Environment["ASPNETCORE_URLS"] = urls;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            return StartProcess(psi, urls, environment: null);
        }
        catch (Exception ex)
        {
            Log($"dotnet run failed: {ex.Message}");
            return false;
        }
    }

    private bool StartProcess(ProcessStartInfo psi, string urls, string? environment)
    {
        if (environment is not null)
        {
            psi.Environment["ASPNETCORE_URLS"] = urls;
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        }

        // Keep CreateNoWindow but capture output so failures are not silent.
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log($"Process.Start failed for {psi.FileName}: {ex.Message}");
            return false;
        }

        if (process is null)
        {
            Log($"Process.Start returned null for {psi.FileName}");
            return false;
        }

        AttachLogPumps(process);

        // Detect immediate crash (missing runtime, bad args, etc.)
        Thread.Sleep(400);
        if (process.HasExited)
        {
            Log($"Process exited immediately (pid={process.Id}, code={process.ExitCode}): {psi.FileName}");
            return false;
        }

        lock (_lock)
            _startedPids.Add(process.Id);

        Log($"Started pid={process.Id}: {psi.FileName} {psi.Arguments}");
        return true;
    }

    private void AttachLogPumps(Process process)
    {
        try
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Log($"[{process.Id}:out] {e.Data}");
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    Log($"[{process.Id}:err] {e.Data}");
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Log($"Could not attach log pumps: {ex.Message}");
        }
    }

    private static ProcessStartInfo? BuildDevExeStartInfo(string projectDir, string csprojName, string urls)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(csprojName);
        var candidates = new[]
        {
            Path.Combine(projectDir, "bin", "Debug", "net8.0", $"{assemblyName}.exe"),
            Path.Combine(projectDir, "bin", "Release", "net8.0", $"{assemblyName}.exe"),
            Path.Combine(projectDir, "bin", "Debug", "net8.0-windows", $"{assemblyName}.exe"),
            Path.Combine(projectDir, "bin", "Release", "net8.0-windows", $"{assemblyName}.exe")
        };
        var builtExe = candidates.FirstOrDefault(File.Exists);
        if (builtExe is null)
            return null;

        return new ProcessStartInfo
        {
            FileName = builtExe,
            Arguments = $"--urls {urls}",
            WorkingDirectory = projectDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    /// <summary>
    /// Directories to search from: ProcessPath folder (install/publish) and BaseDirectory
    /// (important when the host is launched via <c>dotnet</c>).
    /// </summary>
    public static IEnumerable<DirectoryInfo> GetSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<DirectoryInfo>();

        void TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                var full = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!Directory.Exists(full) || !seen.Add(full))
                    return;
                roots.Add(new DirectoryInfo(full));
            }
            catch
            {
                // ignore bad paths
            }
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) &&
                !Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                TryAdd(Path.GetDirectoryName(processPath));
            }
        }
        catch
        {
            // ignore
        }

        TryAdd(AppContext.BaseDirectory);

        try
        {
            var asm = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrWhiteSpace(asm))
                TryAdd(Path.GetDirectoryName(asm));
        }
        catch
        {
            // ignore
        }

        return roots;
    }

    /// <summary>
    /// Resolve published sibling exe: ..\{folder}\{exe} from Desktop base dir,
    /// or walk up looking for an install/repo root that contains the folder.
    /// </summary>
    public static string? FindSiblingPublishedExe(string folderName, string exeName)
    {
        foreach (var start in GetSearchRoots())
        {
            var dir = start;
            while (dir is not null)
            {
                if (dir.Parent is not null)
                {
                    var sibling = Path.Combine(dir.Parent.FullName, folderName, exeName);
                    if (File.Exists(sibling))
                        return sibling;
                }

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

                // Publish folder named "Desktop" (capital D) next to backend/web
                if (dir.Name.Equals("Desktop", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
                {
                    var candidate = Path.Combine(dir.Parent.FullName, folderName, exeName);
                    if (File.Exists(candidate))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Walk up from the Desktop exe directory until a sibling <paramref name="folderName"/>
    /// containing <paramref name="csprojName"/> is found (repo root layout).
    /// </summary>
    public static string? FindSiblingProjectDir(string folderName, string csprojName)
    {
        foreach (var start in GetSearchRoots())
        {
            var dir = start;
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, folderName);
                var csproj = Path.Combine(candidate, csprojName);
                if (Directory.Exists(candidate) && File.Exists(csproj))
                    return candidate;

                if (dir.Name.Equals("desktop", StringComparison.OrdinalIgnoreCase) && dir.Parent is not null)
                {
                    candidate = Path.Combine(dir.Parent.FullName, folderName);
                    csproj = Path.Combine(candidate, csprojName);
                    if (Directory.Exists(candidate) && File.Exists(csproj))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private void SetError(string message)
    {
        LastError = message;
        Log("ERROR: " + message);
    }

    private void Log(string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, line, Encoding.UTF8);
        }
        catch
        {
            // never throw from logging
        }
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
