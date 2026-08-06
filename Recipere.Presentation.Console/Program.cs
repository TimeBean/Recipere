using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Recipere.Application.Get;
using Recipere.Core.Repository;
using Recipere.Infrastructure.Repository;

var services = new ServiceCollection();

services.AddLogging();
services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(GetRequest).Assembly));
services.Configure<YtDlpOptions>(_ => { });
services.AddSingleton<IContentRepository, YtDlpContentRepository>();
services.AddSingleton<IVideoStorage, InMemoryVideoStorage>();

await using var provider = services.BuildServiceProvider();

var mediator = provider.GetRequiredService<ISender>();

var content = await mediator.Send(new GetRequest("https://www.youtube.com/watch?v=O6B7X6R0_Sg"));

Console.WriteLine(content.Title + " " + content.Channel.Name);
