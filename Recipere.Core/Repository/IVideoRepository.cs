using Recipere.Core.Model;

namespace Recipere.Core.Repository;

public interface IVideoRepository
{
    Task<Content> GetAsync(string url, int maxHeight, CancellationToken cancellationToken = default);
    Task<Content> GetMetadataAsync(string url, CancellationToken cancellationToken = default);
    Task<VideoPreview> PreviewAsync(string url, CancellationToken cancellationToken = default);
    Task RemoveAsync(string handle, CancellationToken cancellationToken = default);
    Task<bool> HasAsync(string handle, CancellationToken cancellationToken = default);
}
