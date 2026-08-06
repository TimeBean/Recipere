using System.Collections.Concurrent;
using Recipere.Core.Repository;

namespace Recipere.Infrastructure.Repository;

public sealed class InMemoryVideoStorage : IVideoStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _videos = new();

    public async Task<string> SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var handle = Guid.NewGuid().ToString("N");
        _videos[handle] = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return handle;
    }

    public Task<Stream> OpenAsync(string handle, CancellationToken cancellationToken = default)
    {
        if (!_videos.TryGetValue(handle, out var bytes))
        {
            throw new InvalidOperationException($"Video '{handle}' is not stored.");
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string handle, CancellationToken cancellationToken = default)
    {
        _videos.TryRemove(handle, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ContainsAsync(string handle, CancellationToken cancellationToken = default) =>
        Task.FromResult(_videos.ContainsKey(handle));
}