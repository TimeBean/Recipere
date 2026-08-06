namespace Recipere.Core.Repository;

public interface IVideoStorage
{
    Task<string> SaveAsync(string filePath, CancellationToken cancellationToken = default);
    Task<Stream> OpenAsync(string handle, CancellationToken cancellationToken = default);
    Task DeleteAsync(string handle, CancellationToken cancellationToken = default);
    Task<bool> ContainsAsync(string handle, CancellationToken cancellationToken = default);
}
