using MediatR;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Application.GetMetadata;

public class GetMetadataRequestHandler(IContentRepository repository)
    : IRequestHandler<GetMetadataRequest, Content>
{
    public Task<Content> Handle(GetMetadataRequest request, CancellationToken cancellationToken) =>
        repository.GetMetadataAsync(request.Url, cancellationToken);
}
