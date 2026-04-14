using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that a list of FHIR Search Types.
/// </summary>
public class GetSearchTypeList : ICandleMcpTool
{
    private const string _name = "getSearchTypeList";
    private const string _description = "Gets the list of FHIR search types (not FHIR Data Types).";

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
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the search type list tool.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool, expected to contain resource name parameters.</param>
    /// <param name="storeName">The name of the FHIR store to query. Ignored.</param>
    /// <param name="resourceName">The name of the FHIR resource type from an argument. Ignored.</param>
    /// <param name="store">The FHIR store instance to retrieve the definition from. Ignored.</param>
    /// <returns>
    /// A <see cref="CallToolResult"/> containing:
    /// - The list of the known search types
    /// </returns>
    public CallToolResult RunTool(
        IDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        return CommonCandleMcp.GetResponse(SearchTypeData.SearchTypeDescriptionList.Select(r => r.Code).Order());
    }
}
