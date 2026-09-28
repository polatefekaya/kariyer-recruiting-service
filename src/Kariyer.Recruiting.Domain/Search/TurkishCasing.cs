namespace Kariyer.Recruiting.Domain.Search;

/// <summary>
/// Folds a search term so "Şimşek" and "Simsek" find the same candidate (technical document §10,
/// arama davranışı).
///
/// The four dotted/dotless letters are mapped explicitly instead of relying on a Turkish culture
/// being installed: the same code runs in a container that may or may not carry ICU, and a
/// search that silently stops matching in production is the worst possible way to find that out.
/// Diacritics are stripped as well, because recruiters type candidate names from memory and
/// without a Turkish keyboard as often as with one.
/// </summary>
public static class TurkishCasing
{
    private static readonly Dictionary<char, char> Fold = new()
    {
        ['İ'] = 'i', ['I'] = 'i', ['ı'] = 'i',
        ['Ş'] = 's', ['ş'] = 's',
        ['Ğ'] = 'g', ['ğ'] = 'g',
        ['Ü'] = 'u', ['ü'] = 'u',
        ['Ö'] = 'o', ['ö'] = 'o',
        ['Ç'] = 'c', ['ç'] = 'c',
        ['Â'] = 'a', ['â'] = 'a',
        ['Î'] = 'i', ['î'] = 'i',
        ['Û'] = 'u', ['û'] = 'u',
    };

    /// <summary>Lower-cased and diacritic-free; safe to compare with ordinal equality.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            buffer[i] = Fold.TryGetValue(c, out char folded) ? folded : char.ToLowerInvariant(c);
        }

        return new string(buffer);
    }
}
