using MediatR;
using Recipere.Core.Repository;

namespace Recipere.Application.Remove;

public class RemoveRequestHandler(IContentRepository repository) : IRequestHandler<RemoveRequest>
{
    public Task Handle(RemoveRequest request, CancellationToken cancellationToken)
        => repository.RemoveAsync(request.Url, cancellationToken);
}
