using System.Text.Json;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// MCP tool for validating FHIR search requests against a resource type in the store.
/// This tool validates search parameters and their values to ensure they are valid
/// for the specified FHIR resource type.
/// </summary>
public class ValidateTypeSearch : ICandleMcpTool
{
    /// <summary>
    /// The name of the tool as exposed in the MCP interface.
    /// </summary>
    private const string _name = "validateTypeSearch";
    
    /// <summary>
    /// The description of what the tool does.
    /// </summary>
    private const string _description = "Validate a FHIR search request against a resource type in the store.";

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the description of what the tool does.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// The argument name for the search string parameter.
    /// </summary>
    private const string _searchStringArg = "searchString";

    /// <summary>
    /// The MCP tool definition for this tool.
    /// </summary>
    private static Tool _tool = new()
    {
        Name = _name,
        Description = _description,
        InputSchema = JsonSerializer.Deserialize<JsonElement>($$$"""
            {
                "type": "object",
                "properties": {
                    {{{CommonCandleMcp.StoreNameToolProp}}},
                    {{{CommonCandleMcp.ResourceNameToolProp}}},
                    "{{{_searchStringArg}}}": {
                        "type": "string",
                        "description": "FHIR type search request to validate"
                    }
                },
                "required": ["{{{CommonCandleMcp.StoreNameArgName}}}", "{{{CommonCandleMcp.ResourceNameArgName}}}", "searchString" ]
            }
            """),
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the tool to validate a FHIR search request against a resource type.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool, containing the search string to validate.</param>
    /// <param name="storeName">The name of the FHIR store to operate on.</param>
    /// <param name="resourceName">The name of the FHIR resource type to validate against.</param>
    /// <param name="store">The FHIR store instance to use for validation.</param>
    /// <returns>A <see cref="CallToolResponse"/> containing the validation results, including overall status and individual search parameter validation details.</returns>
    public CallToolResponse RunTool(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        if (storeName == null)
        {
            return CommonCandleMcp.GetResponse(CommonCandleMcp.StoreNameArgRequired);
        }

        if (store == null)
        {
            return CommonCandleMcp.GetResponse(CommonCandleMcp.StoreNotResolved);
        }

        if (resourceName == null)
        {
            return CommonCandleMcp.GetResponse(CommonCandleMcp.ResourceNameArgRequired);
        }

        if (!store.ContainsKey(resourceName))
        {
            return CommonCandleMcp.GetResponse(CommonCandleMcp.ResourceNotResolved);
        }

        string? searchString = null;
        if (arguments?.TryGetValue(_searchStringArg, out JsonElement je) == true)
        {
            searchString = je.GetString();
        }

        if (searchString == null)
        {
            return CommonCandleMcp.GetResponse("Search string is missing or not provided and is required");
        }

        List<string> responses = [];
        (string overallMessage, List<(string SpName, string SpValue, bool IsOk, string Message)> results) = store.ValidateTypeSearchRequest(resourceName, searchString);

        responses.Add(overallMessage);

        foreach ((string spName, string spValue, bool isOk, string message) in results)
        {
            responses.Add($"'{spName}={spValue}': {(isOk ? "Valid" : "Invalid")} - {message}`");
        }

        return CommonCandleMcp.GetResponse(responses);
    }
}
