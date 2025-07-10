using System.Text.Json;
using fhir.candle.McpTools;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that retrieves the list of resources in a FHIR store.
/// </summary>
public class GetDataTypeList : ICandleMcpTool
{
    private const string _name = "getDataTypeList";
    private const string _description = "Gets the list of FHIR data types known in the specified resource store.";

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
                    {{{CommonCandleMcp.StoreNameToolProp}}}
                },
                "required": ["{{{CommonCandleMcp.StoreNameArgName}}}"]
            }
            """),
    };

    /// <summary>
    /// Gets the MCP tool definition for this tool.
    /// </summary>
    public Tool McpTool => _tool;

    /// <summary>
    /// Executes the tool to retrieve the list of FHIR data types known by the specified store.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool (not used by this tool).</param>
    /// <param name="storeName">The name of the FHIR store to retrieve resource types from.</param>
    /// <param name="resourceName">The name of the FHIR resource type (not used by this tool).</param>
    /// <param name="store">The FHIR store instance to query for supported resource types.</param>
    /// <returns>
    /// A <see cref="CallToolResponse"/> containing either:
    /// - An ordered list of supported FHIR resource type names if successful
    /// - An error message if the store name is not provided or the store cannot be resolved
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

        List<string> datatypes = [];
        foreach (FhirTypeData.TypeDescriptionRec rec in FhirTypeData.TypeDescriptionList)
        {
            if (store.Config.FhirVersion < rec.MinFhirVersion)
            {
                continue;
            }

            if (store.Config.FhirVersion > rec.MaxFhirVersion)
            {
                continue;
            }

            datatypes.Add(rec.Name);
        }

        return CommonCandleMcp.GetResponse(datatypes.Order());
    }
}
