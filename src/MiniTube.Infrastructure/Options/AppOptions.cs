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

    /// <summary>
    /// Folder for files that must NEVER be served statically: original uploads and HLS output.
    /// Must not be inside <see cref="RootPath"/>.
    /// </summary>
    public string PrivateRootPath { get; set; } = "storage-private";

    public static string ResolveRoot(string rootPath, string contentRoot) =>
        Path.IsPathRooted(rootPath)
            ? Path.GetFullPath(rootPath)
            : Path.GetFullPath(Path.Combine(contentRoot, rootPath));
}

public class VideoOptions
{
    public long MaxUploadBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB
    public string[] AllowedExtensions { get; set; } = [".mp4", ".mov", ".m4v", ".mkv", ".webm", ".avi"];

    /// <summary>"ffmpeg" works when it is on PATH; otherwise put the full path to ffmpeg.exe here.</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>x264 speed/quality trade-off: ultrafast ... veryfast ... medium ... slow.</summary>
    public string X264Preset { get; set; } = "veryfast";

    /// <summary>How many videos are transcoded at the same time. FFmpeg is already multi-threaded, so 1 is usually right.</summary>
    public int MaxConcurrency { get; set; } = 1;

    public int TimeoutMinutes { get; set; } = 180;
    public int MaxDurationMinutes { get; set; } = 240;

    /// <summary>Keep the uploaded original after processing (lets you re-encode later, costs disk space).</summary>
    public bool KeepOriginal { get; set; } = true;
}
