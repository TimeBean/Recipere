namespace Recipere.Infrastructure.Repository;

public sealed class YtDlpException : Exception
{
    public YtDlpException(string message) : base(message)
    {
    }

    public YtDlpException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
