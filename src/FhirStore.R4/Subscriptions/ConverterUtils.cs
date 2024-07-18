// <copyright file="ConverterUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;

namespace FhirCandle.Subscriptions;

/// <summary>FHIR R4 Subscriptions converter utilities.</summary>
internal static class ConverterUtils
{
    /// <summary>(Immutable) The Base URL of R5 SubscriptionTopic cross-version extensions.</summary>
    internal const string _urlSt5 = "http://hl7.org/fhir/5.0/StructureDefinition/extension-SubscriptionTopic.";

    /// <summary>(Immutable) The URL backport.</summary>
    internal const string _urlBackport = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";

    /// <summary>Parse extensions.</summary>
    /// <param name="extensions">The extensions.</param>
    /// <param name="values">    [out] The extension values.</param>
    /// <param name="nested">    [out] The nested extension values.</param>
    internal static void ParseExtensions(
        IEnumerable<Hl7.Fhir.Model.Extension> extensions,
        out Dictionary<string, List<Hl7.Fhir.Model.DataType>> values,
        out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nested)
    {
        values = new();
        nested = new();

        foreach (Hl7.Fhir.Model.Extension ext in extensions)
        {
            if (string.IsNullOrEmpty(ext.Url))
            {
                continue;
            }

            string name;

            if (ext.Url.StartsWith(_urlSt5, StringComparison.Ordinal))
            {
                name = ext.Url.Substring(72);
            }
            else if (ext.Url.StartsWith(_urlBackport, StringComparison.Ordinal))
            {
                name = ext.Url.Substring(66);
            }
            else if (ext.Url.StartsWith("http"))
            {
                continue;
            }
            else
            {
                name = ext.Url;
            }

            if (ext.Extension?.Any() ?? false)
            {
                if (!nested.ContainsKey(name))
                {
                    nested.Add(name, new());
                }

                nested[name].Add(ext.Extension);
            }

            if (!values.ContainsKey(name))
            {
                values.Add(name, new());
            }

            if (ext.Value != null)
            {
                values[name].Add(ext.Value);
            }
        }
    }

    /// <summary>Parse parameters.</summary>
    /// <param name="components">The components.</param>
    /// <param name="values">    [out] The values.</param>
    /// <param name="nested">    [out] The nested.</param>
    internal static void ParseParameters(
        IEnumerable<Hl7.Fhir.Model.Parameters.ParameterComponent> components,
        out Dictionary<string, List<Hl7.Fhir.Model.DataType>> values,
        out Dictionary<string, List<List<Hl7.Fhir.Model.Parameters.ParameterComponent>>> nested)
    {
        values = new();
        nested = new();

        foreach (Hl7.Fhir.Model.Parameters.ParameterComponent pc in components)
        {
            if (string.IsNullOrEmpty(pc.Name))
            {
                continue;
            }

            string name = pc.Name;

            if (pc.Part?.Any() ?? false)
            {
                if (!nested.ContainsKey(name))
                {
                    nested.Add(name, new());
                }

                nested[name].Add(pc.Part);
            }

            if (!values.ContainsKey(name))
            {
                values.Add(name, new());
            }

            values[name].Add(pc.Value);
        }
    }

    /// <summary>Gets a single string value from a parsed set.</summary>
    /// <param name="values">The parsed values dictionary.</param>
    /// <param name="name">  The name of the element to retrieve.</param>
    /// <returns>The string or empty if not found.</returns>
    internal static string GetString(Dictionary<string, List<Hl7.Fhir.Model.DataType>> values, string name)
    {
        if (!values.ContainsKey(name))
        {
            return string.Empty;
        }

        switch (values[name].First())
        {
            case ResourceReference valRef:
                return valRef.Reference?.ToString() ?? string.Empty;
        }

        return values[name].First().ToString() ?? string.Empty;
    }

    /// <summary>Gets a single boolean value from a parsed set.</summary>
    /// <param name="values">The parsed values dictionary.</param>
    /// <param name="name">  The name of the element to retrieve.</param>
    /// <returns>The boolean value or false if not found.</returns>
    internal static bool GetBool(Dictionary<string, List<Hl7.Fhir.Model.DataType>> values, string name)
    {
        if (!values.ContainsKey(name))
        {
            return false;
        }

        switch (values[name].First())
        {
            case FhirBoolean vb:
                return vb.Value ?? false;
        }

        if (bool.TryParse(values[name].First().ToString() ?? string.Empty, out bool val))
        {
            return val;
        }

        return false;
    }

    /// <summary>Gets al string value from a parsed set.</summary>
    /// <param name="extensions">The parsed values dictionary.</param>
    /// <param name="name">      The name of the element to retrieve.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the strings in this collection.
    /// </returns>
    internal static IEnumerable<string> GetStrings(Dictionary<string, List<Hl7.Fhir.Model.DataType>> extensions, string name)
    {
        if (!extensions.ContainsKey(name))
        {
            return Array.Empty<string>();
        }

        switch (extensions[name].First())
        {
            case ResourceReference valRef:
                return extensions[name].Select(e => (e as ResourceReference)?.Reference ?? string.Empty);
        }

        return extensions[name].Select(e => e.ToString() ?? string.Empty);
    }
}
