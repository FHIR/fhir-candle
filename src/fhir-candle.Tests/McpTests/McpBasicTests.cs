using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using fhir.candle.McpTools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shouldly;
using Xunit;

namespace fhir.candle.Tests.McpTests;

public class McpBasicTests
{
    [Fact]
    public async Task FhirMcpTools_ListTools_ReturnsExpectedTools()
    {
        // Arrange
        var mcp = new FhirMcpTools();

        // Act
        ListToolsResult result = await mcp.HandleListToolsRequest(null!, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Tools.ShouldNotBeNull();
        result.Tools.Count.ShouldBeGreaterThan(0);

        var names = result.Tools.Select(t => t.Name).ToHashSet();

        // Core tool set
        names.ShouldContain("getStoreList");
        names.ShouldContain("getDataTypeList");
        names.ShouldContain("getDataTypeDefinition");
        names.ShouldContain("getResourceList");
        names.ShouldContain("getResourceDefinition");
        names.ShouldContain("getSearchTypeList");
        names.ShouldContain("getSearchTypeDefinition");
        names.ShouldContain("getSearchParameters");
        names.ShouldContain("validateTypeSearch");

        // Basic sanity: name/description present
        foreach (var tool in result.Tools)
        {
            tool.Name.ShouldNotBeNullOrWhiteSpace();
            tool.Description.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void FhirMcpTools_CanBeConstructed()
    {
        var mcp = new FhirMcpTools();
        mcp.ShouldNotBeNull();
    }
}
