using InTheLoop.Core;
using InTheLoop.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol, so every log line has to go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddInTheLoopCore();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "intheloop", Title = "In The Loop", Version = "1.0.0" };
        options.ServerInstructions = ReviewTools.Instructions;
    })
    .WithStdioServerTransport()
    .WithTools<ReviewTools>();

await builder.Build().RunAsync();
