namespace MiniTube.Application.Common;

public interface IFileStorage
{
    /// <summary>Saves a file and returns its public, relative URL (e.g. /media/avatars/abc.webp).</summary>
    Task<string> SaveAsync(string folder, string fileName, Stream content, CancellationToken ct = default);

    /// <summary>Deletes a file previously returned by <see cref="SaveAsync"/>. Missing files are ignored.</summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);
}
