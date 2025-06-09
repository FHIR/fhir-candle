using System.Diagnostics;
using System.Reflection;
// using ModelContextProtocol.Protocol.Transport;
// using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace fhir.candle.McpTools;

public class FhirStoreMcp
{
    public FhirStoreMcp()
    {
        // Constructor logic here
    }

    // public async Task StartMcpAsync(CancellationToken serverCancellationToken)
    // {
    //     string currentVersion =
    //         FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? "0.0.1";
    //
    //     McpServerOptions options = new()
    //     {
    //         ServerInfo = new Implementation() { Name = "FhirCandleStoreMCP", Version = "1.0.0" },
    //         Capabilities = new ServerCapabilities()
    //         {
    //             Tools = new ToolsCapability()
    //             {
    //                 ListToolsHandler = (request, cancellationToken) =>
    //                     ValueTask.FromResult(new ListToolsResult()
    //                     {
    //                         Tools =
    //                         [
    //                             new Tool()
    //                             {
    //                                 Name = "echo",
    //                                 Description = "Echoes the input back to the client.",
    //                                 InputSchema = JsonSerializer.Deserialize<JsonElement>("""
    //                                     {
    //                                         "type": "object",
    //                                         "properties": {
    //                                           "message": {
    //                                             "type": "string",
    //                                             "description": "The input to echo back"
    //                                           }
    //                                         },
    //                                         "required": ["message"]
    //                                     }
    //                                     """),
    //                             }
    //                         ]
    //                     }),
    //                 CallToolHandler = (request, cancellationToken) =>
    //                 {
    //                     if (request.Params?.Name == "echo")
    //                     {
    //                         if (request.Params.Arguments?.TryGetValue("message", out var message) is not true)
    //                         {
    //                             throw new McpException("Missing required argument 'message'");
    //                         }
    //
    //                         return ValueTask.FromResult(new CallToolResponse()
    //                         {
    //                             Content = [new Content() { Text = $"Echo: {message}", Type = "text" }]
    //                         });
    //                     }
    //
    //                     throw new McpException($"Unknown tool: '{request.Params?.Name}'");
    //                 },
    //             }
    //         },
    //     };
    //
    //     await using IMcpServer server = McpServerFactory.Create(new StreamableHttpServerTransport(), options);
    //     await server.RunAsync(serverCancellationToken);
    // }
}
