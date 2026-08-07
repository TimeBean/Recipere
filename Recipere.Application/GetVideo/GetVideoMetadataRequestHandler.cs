using MediatR;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Application.GetVideo;

public class GetVideoMetadataRequestHandler(IVideoRepository repository)
    : IRequestHandler<GetVideoMetadataRequest, Content>
{
    public Task<Content> Handle(GetVideoMetadataRequest request, CancellationToken cancellationToken) =>
        repository.GetMetadataAsync(request.Url, cancellationToken);
}
