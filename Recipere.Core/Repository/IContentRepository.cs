using Recipere.Core.Model;

namespace Recipere.Core.Repository;

public interface IContentRepository
{
    Task<Content> GetAsync(string url, CancellationToken cancellationToken = default);
    Task<Content> GetMetadataAsync(string url, CancellationToken cancellationToken = default);
    Task RemoveAsync(string url, CancellationToken cancellationToken = default);
    Task<bool> ExistAsync(string url, CancellationToken cancellationToken = default);
    Task<bool> HasAsync(string url, CancellationToken cancellationToken = default);
}
