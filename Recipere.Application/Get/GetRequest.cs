using MediatR;
using Recipere.Core.Model;

namespace Recipere.Application.Get;

public record GetRequest(string Url) : IRequest<Content>;