using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PedeLocal.Application.Common;

public static partial class SlugGenerator
{
    public static string Slugify(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                withoutDiacritics.Append(ch);
            }
        }

        var slug = withoutDiacritics.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        slug = NonAlphaNumericRegex().Replace(slug, "-");
        slug = MultipleDashesRegex().Replace(slug, "-").Trim('-');
        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleDashesRegex();
}
