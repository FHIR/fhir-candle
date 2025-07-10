using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that retrieves information about a FHIR resource type.
/// </summary>
public class GetResourceDefinition : ICandleMcpTool
{
    private const string _name = "getResourceDefinition";
    private const string _description = "Gets the definition of a FHIR resource.";

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
                    {{{CommonCandleMcp.ResourceNameToolProp}}}
                },
                "required": ["{{{CommonCandleMcp.ResourceNameArgName}}}"]
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
    /// A <see cref="CallToolResponse"/> containing:
    /// - An error message if the resource name is null or the resource is not resolved
    /// - The definition of the requested resource type, if available
    /// </returns>
    public CallToolResponse RunTool(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        if (resourceName == null)
        {
            return CommonCandleMcp.GetResponse(CommonCandleMcp.ResourceNameArgRequired);
        }

        if (FhirResourceData.ResourceDescriptions.TryGetValue(resourceName, out FhirResourceData.ResourceDescriptionRec rdRec))
        {
            return CommonCandleMcp.GetResponse(rdRec.ToString());
        }

        return CommonCandleMcp.GetResponse(CommonCandleMcp.ResourceNotResolved);
    }
}
