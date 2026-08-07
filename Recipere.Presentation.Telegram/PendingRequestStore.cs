using System.Collections.Concurrent;
using Recipere.Core.Model;

namespace Recipere.Presentation.Telegram;

public sealed class PendingRequestStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, PendingRequest> _requests = new();

    public string Add(string url, Content content)
    {
        var token = Guid.NewGuid().ToString("N");
        _requests[token] = new PendingRequest(url, content, DateTimeOffset.UtcNow);
        return token;
    }

    public bool TryGet(string token, out PendingRequest request)
    {
        PurgeExpired();

        if (_requests.TryRemove(token, out var found) && !IsExpired(found))
        {
            request = found;
            return true;
        }

        request = null!;
        return false;
    }

    private void PurgeExpired()
    {
        foreach (var (key, value) in _requests)
        {
            if (IsExpired(value)) _requests.TryRemove(key, out _);
        }
    }

    private static bool IsExpired(PendingRequest request) =>
        DateTimeOffset.UtcNow - request.CreatedAt > Ttl;
}

public sealed record PendingRequest(
    string Url,
    Content Content,
    DateTimeOffset CreatedAt);
