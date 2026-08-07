using MediatR;
using Recipere.Core.Model;

namespace Recipere.Application.GetVideo;

public record GetVideoMetadataRequest(string Url) : IRequest<Content>;
