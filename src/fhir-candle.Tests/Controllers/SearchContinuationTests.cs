// <copyright file="SearchContinuationTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using FhirCandle.Utils;

namespace fhir.candle.Tests.Controllers;

/// <summary>Exercises search continuation links through the in-process HTTP routes.</summary>
[Collection(CompatibilityHostCollection.Name)]
public class SearchContinuationTests
{
    public static IEnumerable<object[]> Versions => LifecycleTestStore.Versions;

    /// <summary>POST form and URL occurrences survive in the canonical GET self and next links.</summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public async Task PostFormNextReplaysAppliedQuery(FhirReleases.FhirSequenceCodes version)
    {
        const string Root = "http://localhost/fhir/pages";
        const string Code = "A+B&C";
        await using CandleWebApplicationFactory host = new(
        [
            new()
            {
                ControllerName = "pages", BaseUrl = Root, FhirVersion = version,
                AllowExistingId = true, AllowCreateAsUpdate = true,
            },
        ]);
        HttpClient client = host.Client;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/fhir+json");
        async Task Put(string resource, string json)
        {
            using StringContent content = new(json, Encoding.UTF8, "application/fhir+json");
            using HttpResponseMessage response = await client.PutAsync(Root + "/" + resource, content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        await Put("CompartmentDefinition/membership", """
            {"resourceType":"CompartmentDefinition","id":"membership","url":"urn:test:membership","name":"Membership",
             "status":"active","code":"Patient","search":true,"resource":[{"code":"Observation","param":["subject"]}]}
            """);
        await Put("Patient/a", """{"resourceType":"Patient","id":"a"}""");
        foreach ((string id, string code, string status) in new[]
        {
            ("first", Code, "final"), ("second", Code, "final"),
            ("other-code", "other", "final"), ("other-status", Code, "preliminary"),
        })
        {
            await Put("Observation/" + id, $$$"""
                {"resourceType":"Observation","id":"{{{id}}}","status":"{{{status}}}","code":{"coding":[{"code":"{{{code}}}"}]},
                 "subject":{"reference":"Patient/a"}}
                """);
        }

        static async Task<JsonObject> Bundle(HttpResponseMessage response)
        {
            using (response)
            {
                string body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, body);
                return JsonNode.Parse(body)!.AsObject();
            }
        }

        foreach ((string post, string get) in new[]
        {
            ("/Observation/_search", "/Observation"), ("/Patient/a/_search", "/Patient/a/*"),
            ("/Patient/a/Observation/_search", "/Patient/a/Observation"), ("/_search", ""),
        })
        {
            List<KeyValuePair<string, string>> parameters =
            [
                new("code", Code), new("code", Code), new("_count", "1"), new("_sort", "_id"), new("_offset", "0"),
            ];
            if (get.Length == 0)
            {
                parameters.Add(new("_type", "Observation"));
            }
            using FormUrlEncodedContent form = new(parameters);
            string getQuery = "status=final&" + await form.ReadAsStringAsync();
            JsonObject page = await Bundle(await client.PostAsync(Root + post + "?status=final", form));
            for (int offset = 0; offset <= 1; offset++)
            {
                Uri CheckLink(string relation, int linkOffset)
                {
                    JsonNode link = Assert.Single(page["link"]!.AsArray(), l => (string?)l!["relation"] == relation)!;
                    Uri uri = new((string)link["url"]!);
                    Assert.Equal(Root + get, uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
                    NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
                    Assert.Equal(new[] { Code, Code }, query.GetValues("code"));
                    Assert.Equal(new[] { "final" }, query.GetValues("status"));
                    Assert.Equal(new[] { "1" }, query.GetValues("_count"));
                    Assert.Equal(new[] { "_id" }, query.GetValues("_sort"));
                    Assert.Equal(new[] { linkOffset.ToString() }, query.GetValues("_offset"));
                    if (get.Length == 0)
                    {
                        Assert.Equal(new[] { "Observation" }, query.GetValues("_type"));
                    }
                    return uri;
                }

                CheckLink("self", offset);
                Assert.Equal(2, (int?)page["total"]);
                JsonNode entry = Assert.Single(page["entry"]!.AsArray())!;
                Assert.Equal("match", (string?)entry["search"]!["mode"]);
                Assert.Equal(offset == 0 ? "first" : "second", (string?)entry["resource"]!["id"]);
                JsonObject direct = await Bundle(await client.GetAsync(Root + get + "?" + getQuery.Replace("_offset=0", $"_offset={offset}")));
                Assert.True(JsonNode.DeepEquals(direct["entry"], page["entry"]));
                if (offset == 0)
                {
                    page = await Bundle(await client.GetAsync(CheckLink("next", 1)));
                }
                else
                {
                    Assert.DoesNotContain(page["link"]!.AsArray(), l => (string?)l!["relation"] == "next");
                }
            }
        }
    }
}
