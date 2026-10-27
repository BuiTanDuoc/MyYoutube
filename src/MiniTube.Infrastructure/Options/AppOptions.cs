namespace MiniTube.Infrastructure.Options;

public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@minitube.local";
    public string FromName { get; set; } = "MiniTube";
}

public class FrontendOptions
{
    /// <summary>Base URL of the React app, used to build links in emails.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5173";
}

public class StorageOptions
{
    /// <summary>Folder on disk. Relative paths are resolved against the content root.</summary>
    public string RootPath { get; set; } = "storage";

    /// <summary>URL prefix under which the folder is served.</summary>
    public string RequestPath { get; set; } = "/media";

    public static string ResolveRoot(string rootPath, string contentRoot) =>
        Path.IsPathRooted(rootPath)
            ? Path.GetFullPath(rootPath)
            : Path.GetFullPath(Path.Combine(contentRoot, rootPath));
}
