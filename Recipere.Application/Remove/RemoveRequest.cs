using MediatR;

namespace Recipere.Application.Remove;

public record RemoveRequest(string Handle) : IRequest;