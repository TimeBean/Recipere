using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Recipere.Application.Get;
using Recipere.Application.GetVideo;
using Recipere.Core.Repository;
using Recipere.Infrastructure.Repository;

var services = new ServiceCollection();

services.AddLogging();
services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(GetRequest).Assembly));
services.Configure<YtDlpOptions>(_ => { });
services.AddSingleton<IContentRepository, YtDlpContentRepository>();
services.AddSingleton<IVideoRepository, YtDlpVideoRepository>();
services.AddSingleton<IVideoStorage, InMemoryVideoStorage>();

await using var provider = services.BuildServiceProvider();

var mediator = provider.GetRequiredService<ISender>();

var url = "https://www.youtube.com/watch?v=O6B7X6R0_Sg";

var audio = await mediator.Send(new GetRequest(url));
Console.WriteLine($"Audio: {audio.Title} / {audio.Channel.Name} / {audio.DurationString}");

var video = await mediator.Send(new GetVideoRequest(url, MaxHeight: 480));
Console.WriteLine($"Video: {video.Title} / {video.Width}x{video.Height} / {video.Extension} / {video.SizeBytes} bytes");
