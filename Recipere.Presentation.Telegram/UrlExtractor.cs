namespace Recipere.Presentation.Telegram;

public static class UrlExtractor
{
    public static bool TryExtract(string text, out string url)
    {
        if (!string.IsNullOrWhiteSpace(text)
            && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var trimmedUri)
            && IsHttpUrl(trimmedUri))
        {
            url = trimmedUri.ToString();
            return true;
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Uri.TryCreate(word, UriKind.Absolute, out var uri) && IsHttpUrl(uri))
            {
                url = uri.ToString();
                return true;
            }
        }

        url = string.Empty;
        return false;
    }

    private static bool IsHttpUrl(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
