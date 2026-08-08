using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneC.Cli;

// Ensure Cyrillic output is displayed correctly in the Windows console.
Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

// Load local (gitignored) settings with connection strings.
builder.Configuration.SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddCliDi();

using var host = builder.Build();

var app = host.Services.GetRequiredService<CliApp>();
await app.RunAsync(args);