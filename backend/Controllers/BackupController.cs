using BlocksPlant.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize(Roles = "Owner")]
public class BackupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public BackupController(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _env = env;
    }

    /// <summary>Download a consistent copy of the SQLite database.</summary>
    [HttpGet]
    public async Task<IActionResult> Download()
    {
        var dbPath = ResolveDbPath();
        if (!System.IO.File.Exists(dbPath))
            return NotFound(new { message = "Database file not found." });

        // Checkpoint WAL so the main file is complete, then copy to a temp file for download.
        await _db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");

        var tempPath = Path.Combine(Path.GetTempPath(), $"blocksplant-backup-{Guid.NewGuid():N}.db");
        System.IO.File.Copy(dbPath, tempPath, overwrite: true);

        var downloadName = $"blocksplant-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
        try { System.IO.File.Delete(tempPath); } catch { /* ignore */ }

        return File(bytes, "application/octet-stream", downloadName);
    }

    /// <summary>
    /// Replace the live SQLite database with an uploaded backup.
    /// WARNING: overwrites all current data. API should be restarted afterward if connections linger.
    /// </summary>
    [HttpPost("restore")]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Restore(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Backup .db file is required." });
        if (!file.FileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "File must be a .db SQLite backup." });

        var dbPath = ResolveDbPath();
        var dir = Path.GetDirectoryName(dbPath)!;
        Directory.CreateDirectory(dir);

        var uploadPath = Path.Combine(Path.GetTempPath(), $"blocksplant-restore-{Guid.NewGuid():N}.db");
        await using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        // Basic sanity: can we open it as SQLite?
        try
        {
            await using var test = new SqliteConnection($"Data Source={uploadPath}");
            await test.OpenAsync();
            await using var cmd = test.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
            _ = await cmd.ExecuteScalarAsync();
        }
        catch
        {
            try { System.IO.File.Delete(uploadPath); } catch { /* ignore */ }
            return BadRequest(new { message = "Uploaded file is not a valid SQLite database." });
        }

        // Close EF connections before replacing the file
        await _db.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (System.IO.File.Exists(dbPath))
            System.IO.File.Copy(dbPath, Path.Combine(dir, $"blocksplant.pre-restore-{stamp}.db"), overwrite: true);

        // Remove WAL/SHM companions so they don't conflict with restored main file
        foreach (var companion in new[] { dbPath + "-wal", dbPath + "-shm" })
        {
            if (System.IO.File.Exists(companion))
                System.IO.File.Delete(companion);
        }

        System.IO.File.Copy(uploadPath, dbPath, overwrite: true);
        try { System.IO.File.Delete(uploadPath); } catch { /* ignore */ }

        return Ok(new
        {
            message = "Database restored. Restart the API process if anything looks stale. A pre-restore copy was saved beside the database."
        });
    }

    private string ResolveDbPath()
    {
        var cs = _config.GetConnectionString("DefaultConnection") ?? "Data Source=blocksplant.db";
        var builder = new SqliteConnectionStringBuilder(cs);
        var dataSource = builder.DataSource;
        if (Path.IsPathRooted(dataSource))
            return dataSource;
        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, dataSource));
    }
}
