using MediatR;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Application.GetVideo;

public class GetVideoRequestHandler(IVideoRepository repository) : IRequestHandler<GetVideoRequest, Content>
{
    public Task<Content> Handle(GetVideoRequest request, CancellationToken cancellationToken) =>
        repository.GetAsync(request.Url, request.MaxHeight, cancellationToken);
}
