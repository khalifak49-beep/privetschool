namespace SchoolSys.Services;

public interface IFileStorageService
{
    /// <summary>يحفظ ملفاً ويعيد المسار النسبي القابل للعرض (مثل /uploads/students/xxx.jpg).</summary>
    Task<string?> SaveAsync(IFormFile? file, string folder, string[]? allowedExtensions = null, long maxBytes = 8 * 1024 * 1024);

    void Delete(string? relativePath);

    static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    static readonly string[] DocumentExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".zip"];
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IWebHostEnvironment env, ILogger<FileStorageService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string?> SaveAsync(IFormFile? file, string folder,
        string[]? allowedExtensions = null, long maxBytes = 8 * 1024 * 1024)
    {
        if (file is null || file.Length == 0) return null;

        if (file.Length > maxBytes)
            throw new InvalidOperationException($"حجم الملف يتجاوز الحد المسموح ({maxBytes / 1024 / 1024} ميجابايت).");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = allowedExtensions ?? IFileStorageService.DocumentExtensions;
        if (!allowed.Contains(ext))
            throw new InvalidOperationException($"امتداد الملف غير مسموح به: {ext}");

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var safeFolder = string.Join("", folder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/'));
        var dir = Path.Combine(webRoot, "uploads", safeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(dir, name);

        await using (var stream = new FileStream(full, FileMode.Create))
            await file.CopyToAsync(stream);

        return $"/uploads/{safeFolder}/{name}";
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/")) return;

        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var full = Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر حذف الملف {Path}", relativePath);
        }
    }
}
