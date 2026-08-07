using MediatR;
using Recipere.Core.Model;

namespace Recipere.Application.GetVideo;

public record GetVideoRequest(string Url, int MaxHeight) : IRequest<Content>;
