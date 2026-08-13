using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsOrder.Application.Common;
using WhatsOrder.Infrastructure.Common;

namespace WhatsOrder.Infrastructure.Files;

/// <summary>
/// Stores uploads under wwwroot/uploads (served as static files). Swap for S3/Azure Blob
/// by re-implementing IFileStorage — nothing else changes.
/// </summary>
public class LocalFileStorage(IOptions<FileStorageOptions> options, ILogger<LocalFileStorage> logger) : IFileStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private readonly string _rootPath = options.Value.RootPath;

    public async Task<string> SaveAsync(Stream content, string extension, string subfolder, CancellationToken ct = default)
    {
        if (!extension.StartsWith('.'))
            extension = "." + extension;
        if (!AllowedExtensions.Contains(extension))
            throw new BusinessRuleException("invalid_file_type", "Only JPG, PNG, WEBP and GIF images are allowed.");

        var safeSubfolder = Path.GetFileName(subfolder); // no traversal
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var directory = Path.Combine(_rootPath, "uploads", safeSubfolder);
        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, fileName);
        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, ct);
        }

        return $"/uploads/{safeSubfolder}/{fileName}";
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            var relative = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relative));

            // Never delete outside the uploads root.
            var uploadsRoot = Path.GetFullPath(Path.Combine(_rootPath, "uploads"));
            if (fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete file {Path}", relativePath);
        }
        return Task.CompletedTask;
    }
}
