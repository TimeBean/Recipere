using MediatR;
using Recipere.Core.Model;
using Recipere.Core.Repository;

namespace Recipere.Application.Get;

public class GetRequestHandler(IContentRepository repository) : IRequestHandler<GetRequest, Content>
{
    public Task<Content> Handle(GetRequest request, CancellationToken cancellationToken) =>
        repository.GetAsync(request.Url, cancellationToken);
}
