using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniTube.Infrastructure.Options;

namespace MiniTube.Infrastructure.Videos;

public sealed record ProbeResult(int Width, int Height, double DurationSeconds, bool HasAudio);

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErrTail);

/// <summary>Thin wrapper around the ffmpeg / ffprobe executables (arguments are passed as a list, never through a shell).</summary>
public sealed class FfmpegTools
{
    // Containers we accept. This also rejects things like "hls"/"concat" playlists, which
    // can make FFmpeg read local files or call out to URLs.
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "mov", "mp4", "m4a", "3gp", "matroska", "webm", "avi", "mpegts", "mpeg", "flv", "asf", "ogg"
    };

    private readonly VideoOptions _o;
    private readonly ILogger<FfmpegTools> _logger;

    public FfmpegTools(IOptions<VideoOptions> options, ILogger<FfmpegTools> logger)
    {
        _o = options.Value;
        _logger = logger;
    }

    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct)
    {
        try
        {
            var a = await RunAsync(_o.FfmpegPath, ["-hide_banner", "-version"], ct);
            var b = await RunAsync(_o.FfprobePath, ["-hide_banner", "-version"], ct);
            return a.ExitCode == 0 && b.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "FFmpeg is not available. Install FFmpeg and add it to PATH, or set Video:FfmpegPath / Video:FfprobePath.");
            return false;
        }
    }

    public async Task<ProbeResult> ProbeAsync(string path, CancellationToken ct)
    {
        var result = await RunAsync(_o.FfprobePath,
        [
            "-v", "error",
            "-protocol_whitelist", "file",
            "-print_format", "json",
            "-show_format", "-show_streams",
            "-i", path
        ], ct);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("ffprobe failed: {Error}", result.StdErrTail);
            throw new VideoProcessingException("The file is not a valid video.");
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(result.StdOut); }
        catch (JsonException) { throw new VideoProcessingException("The file is not a valid video."); }

        using (doc)
        {
            var root = doc.RootElement;

            var formatName = "";
            double duration = 0;
            if (root.TryGetProperty("format", out var fmt))
            {
                if (fmt.TryGetProperty("format_name", out var fn)) formatName = fn.GetString() ?? "";
                duration = ReadDuration(fmt);
            }

            if (!formatName.Split(',').Any(AllowedFormats.Contains))
                throw new VideoProcessingException("Unsupported video container format.");

            int width = 0, height = 0;
            var hasAudio = false;
            double rotation = 0;

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var s in streams.EnumerateArray())
                {
                    var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                    if (type == "audio") { hasAudio = true; continue; }
                    if (type != "video" || width > 0 || IsAttachedPicture(s)) continue;

                    width = ReadInt(s, "width");
                    height = ReadInt(s, "height");
                    rotation = ReadRotation(s);
                    if (duration <= 0) duration = ReadDuration(s);
                }
            }

            if (width <= 0 || height <= 0)
                throw new VideoProcessingException("No video stream found in the file.");

            // Phone videos are often stored sideways with a rotation flag; FFmpeg auto-rotates, so swap here too.
            if (Math.Abs((int)Math.Round(rotation)) % 180 == 90)
                (width, height) = (height, width);

            return new ProbeResult(width, height, duration, hasAudio);
        }
    }

    /// <summary>Runs ffmpeg and throws <see cref="VideoProcessingException"/> on a non-zero exit code.</summary>
    public async Task RunFfmpegAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await RunAsync(_o.FfmpegPath, args, ct);
        if (result.ExitCode != 0)
        {
            _logger.LogError("ffmpeg exited with {Code}: {Error}", result.ExitCode, result.StdErrTail);
            throw new VideoProcessingException("The video could not be converted.");
        }
    }

    private static async Task<ProcessResult> RunAsync(string exe, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new Queue<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stdout) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderr)
            {
                stderr.Enqueue(e.Data);
                while (stderr.Count > 25) stderr.Dequeue();
            }
        };

        try { process.Start(); }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot start '{exe}'. Install FFmpeg and add it to PATH, or set Video:FfmpegPath / Video:FfprobePath.", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* already gone */ }
        });

        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();

        string tail;
        lock (stderr) tail = string.Join(Environment.NewLine, stderr);
        string output;
        lock (stdout) output = stdout.ToString();

        return new ProcessResult(process.ExitCode, output, tail);
    }

    private static int ReadInt(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;

    private static double ReadDuration(JsonElement e) =>
        e.TryGetProperty("duration", out var d) &&
        double.TryParse(d.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) &&
        double.IsFinite(v) && v > 0 ? v : 0;

    private static bool IsAttachedPicture(JsonElement stream) =>
        stream.TryGetProperty("disposition", out var disp) &&
        disp.TryGetProperty("attached_pic", out var ap) &&
        ap.ValueKind == JsonValueKind.Number && ap.GetInt32() == 1;

    private static double ReadRotation(JsonElement stream)
    {
        if (stream.TryGetProperty("side_data_list", out var list))
        {
            foreach (var sd in list.EnumerateArray())
            {
                if (sd.TryGetProperty("rotation", out var r) && r.ValueKind == JsonValueKind.Number)
                    return r.GetDouble();
            }
        }

        if (stream.TryGetProperty("tags", out var tags) &&
            tags.TryGetProperty("rotate", out var rot) &&
            double.TryParse(rot.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;

        return 0;
    }
}
