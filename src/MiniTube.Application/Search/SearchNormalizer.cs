using System.Globalization;
using System.Text;
using MiniTube.Domain.Entities;

namespace MiniTube.Application.Search;

/// <summary>
/// Turns text into a lower-case, accent-free, punctuation-free form so that
/// "Hướng dẫn học lập trình" can be found by typing "huong dan hoc lap trinh".
/// </summary>
public static class SearchNormalizer
{
    public const int MaxSearchTextLength = 6000;
    public const int MaxTokens = 8;

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;

        foreach (var ch in decomposed)
        {
            // Drop the combining accent marks produced by FormD (ế -> e + ^ + ´)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            // "đ" has no decomposition, so map it by hand
            var c = ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch);

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                sb.Append(' ');
                lastWasSpace = true;
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Distinct normalised words of a query (max <see cref="MaxTokens"/>).</summary>
    public static List<string> Tokenize(string? query) =>
        Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .Take(MaxTokens)
            .ToList();

    public static void ApplyTo(Video video)
    {
        video.SearchTitle = Normalize(video.Title);

        var text = Normalize($"{video.Title} {video.Description}");
        video.SearchText = text.Length > MaxSearchTextLength ? text[..MaxSearchTextLength] : text;
    }

    public static void ApplyTo(ApplicationUser user) =>
        user.SearchName = Normalize($"{user.DisplayName} {user.UserName}");
}
