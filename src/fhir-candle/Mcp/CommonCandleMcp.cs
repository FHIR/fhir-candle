using System.Runtime.CompilerServices;
using Hl7.Fhir.Model.CdsHooks;
using ModelContextProtocol.Protocol;

namespace fhir.candle.Mcp;

public static class CommonCandleMcp
{
    public const string StoreNameArgName = "store";
    public const string StoreNameToolProp = $$$"""
        "{{{StoreNameArgName}}}": {
            "type": "string",
            "description": "Name of the store or tenant to service this request"
            }
        """;
    public const string StoreNameArgRequired = "Store name is missing or not provided and is required";
    public const string StoreNotResolved = "The provided store name did not resolve into a store on this server";

    public const string ResourceNameArgName = "resourceType";
    public const string ResourceNameToolProp = $$$"""
        "{{{ResourceNameArgName}}}": {
            "type": "string",
            "description": "Name of the FHIR resource type for this request"
            }
        """;
    public const string ResourceNameArgRequired = "Resource name is missing or not provided and is required";
    public const string ResourceNotResolved = "The provided resource name did not resolve into known resource type on this server";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallToolResponse GetResponse(IEnumerable<string> responses) => new CallToolResponse()
    {
        Content = responses.Select(r => new Content()
        {
            Text = r,
            Type = "text",
        }).ToList(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CallToolResponse GetResponse(string response) => new CallToolResponse()
    {
        Content = [
            new Content
            {
                Text = response,
                Type = "text",
            }
        ],
    };
}
