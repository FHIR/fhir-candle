using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that retrieves information about a FHIR search type.
/// </summary>
public class GetSearchTypeDefinition : ICandleMcpTool
{
    private const string _name = "getSearchTypeDefinition";
    private const string _description = "Gets the definition of a FHIR search type.";

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the description of what the tool does.
    /// </summary>
    public string Description => _description;

    private const string _searchTypeArg = "searchType";

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
                    "{{{_searchTypeArg}}}": {
                        "type": "string",
                        "description": "FHIR search type to describe"
                    }
                },
                "required": ["{{{_searchTypeArg}}}"]
            }
            """),
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the resource definition retrieval tool.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool, expected to contain resource name parameters.</param>
    /// <param name="storeName">The name of the FHIR store to query. Must not be null.</param>
    /// <param name="resourceName">The name of the FHIR resource type to get the definition for.</param>
    /// <param name="store">The FHIR store instance to retrieve the definition from. Must not be null.</param>
    /// <returns>
    /// A <see cref="CallToolResult"/> containing:
    /// - An error message if the resource name is null or the resource is not resolved
    /// - The definition of the requested resource type, if available
    /// </returns>
    public CallToolResult RunTool(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        string? searchType = null;
        if (arguments?.TryGetValue(_searchTypeArg, out JsonElement je) == true)
        {
            searchType = je.GetString();
        }

        if (searchType is null)
        {
            return CommonCandleMcp.GetResponse("Search type is missing or not provided and is required");
        }

        if (SearchTypeData.SearchTypeDescriptions.TryGetValue(searchType, out SearchTypeData.FhirSearchTypeDescriptionRec rec))
        {
            return CommonCandleMcp.GetResponse(rec.ToString());
        }

        return CommonCandleMcp.GetResponse("The provided search type did not resolve on this server");
    }
}
