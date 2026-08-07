using MediatR;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Application.GetVideo;

public class GetVideoPreviewRequestHandler(IVideoRepository repository)
    : IRequestHandler<GetVideoPreviewRequest, VideoPreview>
{
    public Task<VideoPreview> Handle(GetVideoPreviewRequest request, CancellationToken cancellationToken) =>
        repository.PreviewAsync(request.Url, cancellationToken);
}
