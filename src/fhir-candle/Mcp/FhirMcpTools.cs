using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using fhir.candle.Mcp;
using fhir.candle.Mcp.CandleTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Org.BouncyCastle.Asn1.X500;

namespace fhir.candle.McpTools;

//[McpServerToolType]
public class FhirMcpTools
{
    //private ILogger<FhirMcpTools> _logger;

    private string CurrentVersion =>
        (FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? "0.0.1") +
        "-" +
        DateTime.UtcNow.ToString("o").Replace(".", string.Empty).Replace("-", string.Empty);

    private McpServerOptions _mcpServerOptions;

    private readonly List<ICandleMcpTool> _candleTools;
    private readonly List<Tool> _mcpTools;
    private readonly Dictionary<string, ICandleMcpTool> _toolDict;

    public FhirMcpTools()
    {
        _mcpServerOptions = new McpServerOptions()
        {
            ServerInfo = new Implementation()
            {
                Name = "FhirCandleMcp",
                Version = CurrentVersion,
            }
        };

        _candleTools = [
            new GetStoreList(),
            new GetDataTypeList(),
            new GetDataTypeDefinition(),
            new GetResourceList(),
            new GetResourceDefinition(),
            new GetSearchTypeList(),
            new GetSearchTypeDefinition(),
            new GetSearchParameters(),
            new ValidateTypeSearch(),
        ];

        _mcpTools = _candleTools.Select(t => t.McpTool).ToList();
        _toolDict = _candleTools.ToDictionary(t => t.Name);
    }


    //private List<Tool> buildToolList()
    //{
    //    // iterate across all the stores
    //    if (FhirStoreManager.IInstance == null)
    //    {
    //        return tools;
    //    }

    //    foreach (IFhirStore store in FhirStoreManager.IInstance!.Values.OrderBy(s => s.Config.ControllerName))
    //    {
    //        // add the store name to the tool name
    //        string storeName = store.Config.ControllerName;

    //        // add the get store URL tool to the list
    //        tools.Add(new Tool()
    //        {
    //            Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameGetStoreUrl}",
    //            Description = $"The base URL for the {storeName} store.",
    //            InputSchema = _getStoreUrlTool.InputSchema,
    //        });

    //        // add the get resource list tool to the list
    //         tools.Add(new Tool()
    //         {
    //             Name = $"{storeName}{_toolNameStoreDelimiter}{_getResourceListToolName}",
    //             Description = $"The resource list for the {storeName} store.",
    //         });

    //        // add the get search parameters for resource tool to the list
    //        tools.Add(new Tool()
    //        {
    //            Name = $"{storeName}{_toolNameStoreDelimiter}{_getSearchParameterListToolName}",
    //            Description = $"The search parameters for a resource in the {storeName} store.",
    //            InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
    //                {
    //                    "type": "object",
    //                    "properties": {
    //                      "{{{_resourceNameArg}}}": {
    //                          "type": "string",
    //                          "description": "Name of the resource type to get search parameters for"
    //                          }
    //                    },
    //                    "required": ["{{{_resourceNameArg}}}"]
    //                }
    //                """),
    //        });

    //        // add the type search validation tool to the list
    //        tools.Add(new Tool()
    //        {
    //            Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameValidateTypeSearch}",
    //            Description = $"Validate a FHIR search request against a resource type in the store.",
    //            InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
    //                {
    //                    "type": "object",
    //                    "properties": {
    //                        "{{{_resourceNameArg}}}": {
    //                            "type": "string",
    //                            "description": "Name of the resource type as the base of the search request"
    //                        },
    //                        "{{{_searchStringArg}}}": {
    //                            "type": "string",
    //                            "description": "FHIR search request to validate"
    //                        }
    //                    },
    //                    "required": ["{{{_resourceNameArg}}}", "{{{_searchStringArg}}}"]
    //                }
    //                """),
    //        });
    //    }

    //    return tools;
    //}

    public ValueTask<ListToolsResult> HandleListToolsRequest(RequestContext<ListToolsRequestParams> request, CancellationToken ct) =>
        new ValueTask<ListToolsResult>(new ListToolsResult() { Tools = _mcpTools, });

    public ValueTask<CallToolResponse> HandleCallToolRequest(
        RequestContext<CallToolRequestParams> request,
        CancellationToken ct)
    {
        if (request.Params?.Name == null)
        {
            return ValueTask.FromResult(CommonCandleMcp.GetResponse("Tool call without a function name specified!."));
        }

        string fnName = request.Params.Name;
        string? storeName = null;

        //int sepLoc = fnName.IndexOf(_toolNameStoreDelimiter, StringComparison.Ordinal);
        //if (sepLoc != -1)
        //{
        //    // store name is first
        //    string[] fnParts = fnName.Split(_toolNameStoreDelimiter, StringSplitOptions.RemoveEmptyEntries);
        //    storeName = fnParts[0];
        //    fnName = fnParts[1];
        //}

        if ((request.Params?.Arguments?.TryGetValue(CommonCandleMcp.StoreNameArgName, out JsonElement storeNameJ) == true))
        {
            storeName = storeNameJ.GetString();
        }

        string? resourceName = null;
        if (request.Params?.Arguments?.TryGetValue(CommonCandleMcp.ResourceNameArgName, out JsonElement resourceTypeJ) == true)
        {
            resourceName = resourceTypeJ.GetString();
        }

        IFhirStore? store = (storeName != null) && (FhirStoreManager.IInstance?.TryGetValue(storeName, out IFhirStore? fhirStore) == true)
            ? fhirStore
            : null;

        if (_toolDict.TryGetValue(fnName, out ICandleMcpTool? tool))
        {
            // use the tool's RunTool method
            return ValueTask.FromResult(tool.RunTool(request.Params?.Arguments, storeName, resourceName, store));
        }

        // fail
        return ValueTask.FromResult(CommonCandleMcp.GetResponse($"Unknown tool: {request.Params?.Name}."));
    }
}
