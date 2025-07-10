using System.Text.Json;
using fhir.candle.Services;
using FhirCandle.Storage;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp.CandleTools;

/// <summary>
/// A Model Context Protocol (MCP) tool that retrieves information about configured FHIR stores.
/// </summary>
public class GetStoreList : ICandleMcpTool
{
    private const string _name = "getStoreList";
    private const string _description = "Gets the list of FHIR stores, the base URL of the FHIR store, and their FHIR Versions that are configured on this server.";

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
    /// Executes the GetStoreList tool to retrieve information about all configured FHIR stores.
    /// </summary>
    /// <param name="arguments">The arguments passed to the tool (not used by this tool).</param>
    /// <param name="storeName">The name of the FHIR store to operate on (not used by this tool).</param>
    /// <param name="resourceName">The name of the FHIR resource type to operate on (not used by this tool).</param>
    /// <param name="store">The FHIR store instance to use for operations (not used by this tool).</param>
    /// <returns>A <see cref="CallToolResponse"/> containing the list of FHIR stores with their URLs and versions.</returns>
    public CallToolResponse RunTool(
        IReadOnlyDictionary<string, JsonElement>? arguments,
        string? storeName,
        string? resourceName,
        IFhirStore? store)
    {
        IEnumerable<string> responses = ((IFhirStoreManager?)FhirStoreManager.Instance)?
            .Values
            .Select(t => $"{t.Config.ControllerName}: URL is {t.Config.BaseUrl}/, FHIR version is {t.Config.FhirVersion}")
            ?? [];

        return CommonCandleMcp.GetResponse(responses);
    }
}
