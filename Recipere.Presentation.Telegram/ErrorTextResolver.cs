namespace Recipere.Presentation.Telegram;

public static class ErrorTextResolver
{
    public static string Resolve(Exception exception, MessageOptions options)
    {
        var message = exception.Message;

        foreach (var cause in options.ErrorCauses)
        {
            foreach (var keyword in cause.Contains)
            {
                if (!string.IsNullOrWhiteSpace(keyword)
                    && message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(cause.Response)
                        ? options.FailureText
                        : cause.Response;
                }
            }
        }

        return options.FailureText;
    }
}
