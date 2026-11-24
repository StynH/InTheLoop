using InTheLoop.Core;
using InTheLoop.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// Default: talk MCP over stdin/stdout, which is what desktop AI clients launch.
// "--http [port]": serve MCP at http://127.0.0.1:<port>/mcp for clients that want a URL.
var httpFlag = Array.IndexOf(args, "--http");
if (httpFlag < 0)
{
    var builder = Host.CreateApplicationBuilder(args);

    // stdout carries the protocol, so every log line has to go to stderr.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services.AddInTheLoopCore();
    builder.Services.AddMcpServer(Configure).WithStdioServerTransport().WithTools<ReviewTools>();

    await builder.Build().RunAsync();
}
else
{
    var port = httpFlag + 1 < args.Length && int.TryParse(args[httpFlag + 1], out var parsed) ? parsed : 5179;

    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

    builder.Services.AddInTheLoopCore();
    builder.Services.AddMcpServer(Configure).WithHttpTransport().WithTools<ReviewTools>();

    var app = builder.Build();
    app.MapMcp("/mcp");

    Console.WriteLine($"In The Loop MCP server listening on http://127.0.0.1:{port}/mcp");
    await app.RunAsync();
}

static void Configure(McpServerOptions options)
{
    options.ServerInfo = new Implementation { Name = "intheloop", Title = "In The Loop", Version = "1.0.0" };
    options.ServerInstructions = ReviewTools.Instructions;
}
