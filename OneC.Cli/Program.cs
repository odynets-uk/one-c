using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneC.Cli;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCliDi();

using var host = builder.Build();

var app = host.Services.GetRequiredService<CliApp>();
await app.RunAsync(args);