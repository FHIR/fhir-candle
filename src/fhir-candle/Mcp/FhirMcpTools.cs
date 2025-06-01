using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
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
    private const string _toolNameStoreDelimiter = "_";

    private const string _toolNameGetResourceList = "getResourceList";
    private const string _toolNameGetStoreList = "getStoreList";
    private const string _toolNameGetStoreUrl = "getStoreUrl";
    private const string _toolNameGetSearchParametersForResource = "getSearchParametersForResource";
    private const string _toolNameGetSearchTypeList = "getSearchTypeList";
    private const string _toolNameGetSearchTypeDefinition = "getSearchTypeDefinition";
    private const string _toolNameValidateTypeSearch = "validateTypeSearch";

    private ILogger<FhirMcpTools> _logger;

    private string CurrentVersion =>
        (FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion?.ToString() ?? "0.0.1") +
        "-" +
        DateTime.UtcNow.ToString("o").Replace(".", string.Empty).Replace("-", string.Empty);

    private McpServerOptions _mcpServerOptions;

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
    }

    private const string _storeNameArg = "store";
    private const string _storeNameToolProp = $$$"""
        "{{{_storeNameArg}}}": {
            "type": "string",
            "description": "Name of the store or tenant to service this request"
            }
        """;

    private const string _resourceNameArg = "resourceType";
//     private const string _resourceNameToolProp = $$$"""
//               "{{{_resourceNameArg}}}": {
//                   "type": "string",
//                   "description": "Name of the resource type for this request"
//                   }
//               """;

    private const string _searchTypeNameArg = "searchType";
    private const string _searchStringArg = "searchString";

    private static Tool _getStoreUrlTool = new()
    {
        Name = _toolNameGetStoreUrl,
        Description = "Get the base FHIR URL for the specified store.",
        InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
            {
                "type": "object",
                "properties": {
                    {{{_storeNameToolProp}}}
                },
                "required": ["{{{_storeNameArg}}}"]
            }
            """),
    };

    private static Tool _getStoreListTool = new()
    {
        Name = _toolNameGetStoreList,
        Description = "Gets the list of FHIR stores, the base URL of the FHIR store, and their FHIR Versions that are configured on this server.",
    };

    private static Tool _getResourceListTool = new()
    {
        Name = _toolNameGetResourceList,
        Description = "Gets the list of FHIR resources supported by the specified resource store.",
        InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
            {
                "type": "object",
                "properties": {
                    {{{_storeNameToolProp}}}
                },
                "required": ["{{{_storeNameArg}}}"]
            }
            """),
    };

    private static Tool _getSearchTypeListTool = new()
    {
        Name = _toolNameGetSearchTypeList,
        Description = "Gets the list of search types supported by this server.",
    };

    private static Tool _getSearchTypeDefinitionTool = new()
    {
        Name = _toolNameGetSearchTypeDefinition,
        Description = "Gets the definition of a search type supported by this server.",
        InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
            {
                "type": "object",
                "properties": {
                    "{{{_searchTypeNameArg}}}": {
                        "type": "string",
                        "description": "Name of the search type to get the definition of"
                    }
                },
                "required": ["{{{_resourceNameArg}}}"]
            }
            """),
    };

    private List<Tool> buildToolList()
    {
        // static tools we provide (require store name as an argument)
        List<Tool> tools = [
            _getStoreUrlTool,
            _getStoreListTool,
            _getResourceListTool,
            _getSearchTypeListTool,
            _getSearchTypeDefinitionTool,
        ];

        // iterate across all the stores
        if (FhirStoreManager.IInstance == null)
        {
            return tools;
        }

        foreach (IFhirStore store in FhirStoreManager.IInstance!.Values.OrderBy(s => s.Config.ControllerName))
        {
            // add the store name to the tool name
            string storeName = store.Config.ControllerName;

            // add the get store URL tool to the list
            tools.Add(new Tool()
            {
                Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameGetStoreUrl}",
                Description = $"The base URL for the {storeName} store.",
                InputSchema = _getStoreUrlTool.InputSchema,
            });

            // add the get resource list tool to the list
             tools.Add(new Tool()
             {
                 Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameGetResourceList}",
                 Description = $"The resource list for the {storeName} store.",
             });

            // add the get search parameters for resource tool to the list
            tools.Add(new Tool()
            {
                Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameGetSearchParametersForResource}",
                Description = $"The search parameters for a resource in the {storeName} store.",
                InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
                    {
                        "type": "object",
                        "properties": {
                          "{{{_resourceNameArg}}}": {
                              "type": "string",
                              "description": "Name of the resource type to get search parameters for"
                              }
                        },
                        "required": ["{{{_resourceNameArg}}}"]
                    }
                    """),
            });

            // add the type search validation tool to the list
            tools.Add(new Tool()
            {
                Name = $"{storeName}{_toolNameStoreDelimiter}{_toolNameValidateTypeSearch}",
                Description = $"Validate a FHIR search request against a resource type in the store.",
                InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
                    {
                        "type": "object",
                        "properties": {
                            "{{{_resourceNameArg}}}": {
                                "type": "string",
                                "description": "Name of the resource type as the base of the search request"
                            },
                            "{{{_searchStringArg}}}": {
                                "type": "string",
                                "description": "FHIR search request to validate"
                            }
                        },
                        "required": ["{{{_resourceNameArg}}}", "{{{_searchStringArg}}}"]
                    }
                    """),
            });
        }

        return tools;
    }

    public async ValueTask<ListToolsResult> HandleListToolsRequest(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken ct)
    {
        return new ListToolsResult() { Tools = buildToolList(), };
    }

    public ValueTask<CallToolResponse> HandleCallToolRequest(
        RequestContext<CallToolRequestParams> request,
        CancellationToken ct)
    {
        IEnumerable<string>? responses = null;
        string? response = null;

        if (request.Params?.Name == null)
        {
            return ValueTask.FromResult(new CallToolResponse()
            {
                Content = [ new()
                {
                    Text = $"Tool call without a function name specified!.",
                    Type = "text",
                }]
            });
        }

        string fnName = request.Params.Name;
        string? storeName = null;

        int sepLoc = fnName.IndexOf(_toolNameStoreDelimiter, StringComparison.Ordinal);
        if (sepLoc != -1)
        {
            // store name is first
            string[] fnParts = fnName.Split(_toolNameStoreDelimiter, StringSplitOptions.RemoveEmptyEntries);
            storeName = fnParts[0];
            fnName = fnParts[1];
        }

        if ((storeName == null) &&
            (request.Params?.Arguments?.TryGetValue(_storeNameArg, out JsonElement storeNameJ) == true))
        {
            storeName = storeNameJ.GetString();
        }

        string? resourceName = null;
        if (request.Params?.Arguments?.TryGetValue(_resourceNameArg, out JsonElement resourceTypeJ) == true)
        {
            resourceName = resourceTypeJ.GetString();
        }

        IFhirStore? store = (storeName != null) && (FhirStoreManager.IInstance?.TryGetValue(storeName, out IFhirStore? fhirStore) == true)
            ? fhirStore
            : null;

        switch (fnName)
        {
            case _toolNameGetStoreUrl:
                response = store?.Config.BaseUrl ?? $"Failed to resolve store named: {storeName}";
                break;

            case _toolNameGetStoreList:
                responses = GetStoreList();
                break;

            case _toolNameGetResourceList:
                {
                    if (store == null)
                    {
                        response = "A store name is required.";
                    }
                    else
                    {
                        responses = GetResourceList(store!);
                    }
                }
                break;

            case _toolNameGetSearchParametersForResource:
                {
                    if (store == null)
                    {
                        response = "A store name is required.";
                    }
                    else
                    {
                        responses = GetSearchParameterList(store!, resourceName);
                    }
                }
                break;

            case _toolNameGetSearchTypeList:
                {
                    responses = GetSearchTypeList();
                }
                break;

            case _toolNameGetSearchTypeDefinition:
                {
                    string? searchTypeName = null;
                    if (request.Params?.Arguments?.TryGetValue(_searchTypeNameArg, out JsonElement searchTypeNameJ) == true)
                    {
                        searchTypeName = searchTypeNameJ.GetString();
                    }

                    response = GetSearchTypeDescription(searchTypeName);
                }
                break;

            case _toolNameValidateTypeSearch:
                {
                    if (store == null)
                    {
                        response = "A store name is required.";
                    }
                    else if (resourceName == null)
                    {
                        response = "A resource type name is required.";
                    }
                    else
                    {
                        if (request.Params?.Arguments?.TryGetValue(_searchStringArg, out JsonElement searchStringJ) == true)
                        {
                            string? searchString = searchStringJ.GetString();
                            responses = ValidateTypeSearch(store!, resourceName, searchString!);
                        }
                        else
                        {
                            response = "A search string is required.";
                        }
                    }
                }
                break;

            default:
                response = $"Unknown tool: {request.Params?.Name}.";
                break;
        }

        if (responses != null)
        {
            return ValueTask.FromResult(new CallToolResponse()
            {
                Content = responses.Select(r => new Content()
                {
                    Text = r,
                    Type = "text",
                }).ToList(),
            });
        }

        if (response != null)
        {
            return ValueTask.FromResult(new CallToolResponse()
            {
                Content = [ new()
                {
                    Text = response,
                    Type = "text",
                }]
            });
        }

        return ValueTask.FromResult(new CallToolResponse()
        {
            Content = [ new()
            {
                Text = $"Tool {request.Params?.Name} completed with no response.",
                Type = "text",
            }]
        });
    }

    private static IEnumerable<string> ValidateTypeSearch(IFhirStore store, string resourceName, string searchString)
    {
        (string overallMessage, List<(string SpName, string SpValue, bool IsOk, string Message)> results) = store.ValidateTypeSearchRequest(resourceName, searchString);

        yield return overallMessage;

        foreach ((string spName, string spValue, bool isOk, string message) in results)
        {
            yield return $"'{spName}={spValue}': {(isOk ? "Valid" : "Invalid")} - {message}`";
        }
    }

    private static IEnumerable<string> GetStoreList()
    {
        return ((IFhirStoreManager?)FhirStoreManager.Instance)?.Values.Select(t => $"{t.Config.ControllerName}: URL is {t.Config.BaseUrl}/, FHIR version is {t.Config.FhirVersion}").ToList() ?? [];
    }

    private static IEnumerable<string> GetResourceList(IFhirStore store)
    {
        return store.Keys.Select(rName =>
            McpData.ResourceDescriptions.TryGetValue(rName, out McpData.ResourceDescriptionRec rdRec)
            ? rdRec.ToString()
            : rName);
    }

    private static IEnumerable<string> GetSearchTypeList()
    {
        return McpData.SearchTypeDescriptions.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value.ToString());
    }

    private static string GetSearchTypeDescription(string? searchTypeName)
    {
        if ((searchTypeName != null) &&
            McpData.SearchTypeDescriptions.TryGetValue(searchTypeName, out McpData.FhirSearchTypeDescriptionRec searchTypeRec))
        {
            return searchTypeRec.ToString();
        }

        return $"Search type '{searchTypeName}' not found.";
    }

    private static IEnumerable<string> GetSearchParameterList(IFhirStore store, string? resourceName)
    {
        List<(string ResourceName, string? Name, string? Code, string? Description, string SearchType)> sps = store.GetSearchParameters(resourceName);

        return sps.Order().Select(spRec =>
            $"{spRec.Code ?? spRec.Name ?? "Unnamed"} ({spRec.SearchType}): {spRec.Description ?? "No description"}");
    }
}
