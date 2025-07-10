using System.Text.Json;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// MCP tool for retrieving FHIR search parameters for a specified resource type in a FHIR store.
/// This tool provides access to search parameter definitions including their names, codes, 
/// descriptions, and search types for any supported FHIR resource type.
/// </summary>
public class GetSearchParameters : ICandleMcpTool
{
    /// <summary>
    /// The name of the tool as exposed in the MCP interface.
    /// </summary>
    private const string _name = "getSearchParameters";

    /// <summary>
    /// The description of what the tool does.
    /// </summary>
    private const string _description = "Get the FHIR Search Parameters for a resource in a specified store.";

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the description of what the tool does.
    /// </summary>
    public string Description => _description;

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
                    {{{CommonCandleMcp.ResourceNameToolProp}}}
                },
                "required": ["{{{CommonCandleMcp.StoreNameArgName}}}", "{{{CommonCandleMcp.ResourceNameArgName}}}" ]
            }
            """),
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the tool to retrieve FHIR search parameters for a specified resource type in a store.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool (not used in this implementation).</param>
    /// <param name="storeName">The name of the FHIR store to query.</param>
    /// <param name="resourceName">The name of the FHIR resource type to get search parameters for.</param>
    /// <param name="store">The FHIR store instance to query for search parameters.</param>
    /// <returns>
    /// A <see cref="CallToolResponse"/> containing either the search parameters formatted as a list
    /// of parameter names with their search types and descriptions, or an error message if the
    /// operation fails due to missing arguments or invalid store/resource references.
    /// </returns>
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

        List<(string ResourceName, string? Name, string? Code, string? Description, string? SearchType)> sps = store.GetSearchParameters(resourceName);


        return CommonCandleMcp.GetResponse(sps
            .Order()
            .Select(spRec => $"{spRec.Code ?? spRec.Name ?? "Unnamed"} ({spRec.SearchType}): {spRec.Description ?? string.Empty}"));
    }
}
