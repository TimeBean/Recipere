using MediatR;
using Recipere.Core.Model;

namespace Recipere.Application.GetVideo;

public record GetVideoPreviewRequest(string Url) : IRequest<VideoPreview>;
