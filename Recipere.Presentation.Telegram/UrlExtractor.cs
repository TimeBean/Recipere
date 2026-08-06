namespace Recipere.Presentation.Telegram;

public static class UrlExtractor
{
    public static bool TryExtract(string text, out string url)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            url = string.Empty;
            return false;
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryCreateUri(word, out var uri))
            {
                url = uri.ToString();
                return true;
            }
        }

        url = string.Empty;
        return false;
    }

    private static bool TryCreateUri(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri) && IsHttpUrl(uri))
        {
            return true;
        }

        if (!value.Contains('.'))
        {
            return false;
        }

        return Uri.TryCreate("https://" + value, UriKind.Absolute, out uri) && IsHttpUrl(uri);
    }

    private static bool IsHttpUrl(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
