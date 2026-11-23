// <copyright file="FhirSortComparer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace FhirCandle.Search;

public class FhirSortComparer(VersionedFhirStore store, ParsedResultParameters.SortRequest[] sortRequests) : IComparer<Resource>
{
    // No PocoNode (and hence no parent resource) survives the projection.
    internal sealed record Key(string Kind, params IComparable?[] Parts);

    internal Key?[] Project(Resource resource)
    {
        PocoNode node = resource.ToPocoNode();
        FhirEvaluationContext context = new()
        {
            Resource = node,
            TerminologyService = store.Terminology,
            ElementResolver = store.Resolve,
        };
        return sortRequests.Select(sr => Project(sr.Compiled.Invoke(node, context).FirstOrDefault())).ToArray();
    }

    private static Key? Project(PocoNode? value)
    {
        if (value is null)
        {
            return null;
        }

        return ((IFhirValueProvider)value).FhirValue switch
        {
            FhirBoolean b => new("boolean", b.Value),
            Integer i => new("integer", (long?)i.Value),
            Integer64 i => new("integer", i.Value),
            UnsignedInt i => new("integer", (long?)i.Value),
            PositiveInt i => new("integer", (long?)i.Value),
            Date d => new("instant", d.ToDateTimeOffset()),
            FhirDateTime d => new("instant", d.ToDateTimeOffset(TimeSpan.Zero)),
            Instant i => new("instant", i.Value),
            Period p => new("period", p.StartElement?.ToDateTimeOffset(TimeSpan.Zero), p.EndElement?.ToDateTimeOffset(TimeSpan.Zero)),
            Time t => new("time", t.Value),
            Quantity q => new("quantity", q.Value),
            HumanName n => new("name", n.Text, n.Family, n.Given.Any() ? string.Join(' ', n.Given) : null),
            Address a => new("address", a.Text, a.City, a.State, a.PostalCode, a.Country),
            ResourceReference r => new("reference", r.Display, r.Reference,
                r.Identifier is null ? null : r.Identifier.System ?? string.Empty,
                r.Identifier is null ? null : r.Identifier.Value ?? string.Empty),
            _ => new("value", value.GetValue() as IComparable ?? value.Poco.ToString()),
        };
    }

    private static int ComparePart(IComparable? x, IComparable? y) =>
        x is not null && y is not null && x.GetType() == y.GetType() ? x.CompareTo(y) : 0;

    internal int CompareKeys(Key?[] left, Key?[] right)
    {
        for (int i = 0; i < sortRequests.Length; i++)
        {
            Key? x = left[i];
            Key? y = right[i];
            int result;
            if (x is null || y is null)
            {
                result = x is null ? y is null ? 0 : -1 : 1;
            }
            else if (x.Kind != y.Kind)
            {
                continue;
            }
            else if (x.Kind == "boolean")
            {
                // The original comparer puts true first and a boolean tie is final.
                result = Equals(x.Parts[0], y.Parts[0]) ? 0 : Equals(x.Parts[0], true) ? -1 : 1;
                return sortRequests[i].Ascending ? result : -result;
            }
            else
            {
                result = 0;
                int length = x.Kind == "reference" ? 2 : x.Parts.Length;
                for (int part = 0; part < length && result == 0; part++)
                {
                    result = ComparePart(x.Parts[part], y.Parts[part]);
                }
                if (result == 0 && x.Kind == "period")
                {
                    result = ComparePart(x.Parts[0], y.Parts[1]);
                    if (result == 0)
                    {
                        result = ComparePart(x.Parts[1], y.Parts[0]);
                    }
                }
                if (result == 0 && x.Kind == "reference" && Equals(x.Parts[2], y.Parts[2]))
                {
                    result = ComparePart(x.Parts[3], y.Parts[3]);
                }
            }
            if (result != 0)
            {
                return sortRequests[i].Ascending ? result : -result;
            }
        }
        return 0;
    }

    public int Compare(Resource? x, Resource? y) =>
        x is null ? y is null ? 0 : -1 : y is null ? 1 : CompareKeys(Project(x), Project(y));
}
