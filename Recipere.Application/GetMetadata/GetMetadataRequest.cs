using MediatR;
using Recipere.Core.Model;

namespace Recipere.Application.GetMetadata;

public record GetMetadataRequest(string Url) : IRequest<Content>;
