namespace WhatsOrder.Infrastructure.Common;

public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public URL of the Angular app — used in password-reset links and share links.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:4200";

    public bool SeedDemoData { get; set; }
}

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Absolute root folder for uploads; the API sets this to wwwroot by default.</summary>
    public string RootPath { get; set; } = string.Empty;
}
