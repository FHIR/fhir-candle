using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that retrieves information about a FHIR data type.
/// </summary>
public class GetDataTypeDefinition : ICandleMcpTool
{
    private const string _name = "getDataTypeDefinition";
    private const string _description = "Gets the definition of a FHIR data type.";

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the description of what the tool does.
    /// </summary>
    public string Description => _description;

    private const string _dataTypeArgName = "datatypeName";

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
                "{{{_dataTypeArgName}}}": {
                    "type": "string",
                    "description": "Name of the FHIR data type for this request"
                    }
                },
                "required": ["{{{_dataTypeArgName}}}"]
            }
            """),
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the type definition retrieval tool.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool, expected to contain data type name parameters.</param>
    /// <param name="storeName">The name of the FHIR store to query. Must not be null.</param>
    /// <param name="resourceName">The name of the FHIR data type to get the definition for.</param>
    /// <param name="store">The FHIR store instance to retrieve the definition from. Must not be null.</param>
    /// <returns>
    /// A <see cref="CallToolResponse"/> containing:
    /// - An error message if the data type name is null or the data type is not resolved
    /// - The definition of the requested data type, if available
    /// </returns>
    public CallToolResponse RunTool(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        string? dtName = null;
        if (arguments?.TryGetValue(_dataTypeArgName, out JsonElement je) == true)
        {
            dtName = je.GetString();
        }

        if (dtName == null)
        {
            return CommonCandleMcp.GetResponse("Data type name is missing or not provided and is required");
        }

        if (FhirTypeData.TypeDescriptions.TryGetValue(dtName, out FhirTypeData.TypeDescriptionRec rec))
        {
            return CommonCandleMcp.GetResponse(rec.ToString());
        }

        return CommonCandleMcp.GetResponse("The provided data type name did not resolve into known type on this server");
    }
}
