// <copyright file="SerializationCommon.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json;
using System.Xml;

namespace FhirCandle.Serialization;

/// <summary>Common serialization utilities.</summary>
public static class SerializationCommon
{
    /// <summary>Serialize object.</summary>
    /// <typeparam name="T">Generic type parameter.</typeparam>
    /// <param name="obj">   The object.</param>
    /// <param name="format">Destination format.</param>
    /// <param name="pretty">If the output should be 'pretty' formatted.</param>
    /// <returns>A string.</returns>
    public static string SerializeObject<T>(
        T obj,
        string format = "application/json",
        bool pretty = false)
    {
#if NET8_0_OR_GREATER
        string[] formatComponents = format.Split(';', StringSplitOptions.TrimEntries);
#else
        string[] formatComponents = format.Split(';').Select(s => s.Trim()).ToArray();
#endif

        System.Text.Encoding encoding = System.Text.Encoding.UTF8;
        switch (formatComponents[0])
        {
            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                {
                    System.Xml.Serialization.XmlSerializer xmlSerializer = new(typeof(T));

                    using (MemoryStream ms = new MemoryStream())
                    using (System.Xml.XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = encoding, Indent = pretty }))
                    {
                        xmlSerializer.Serialize(writer, obj);
                        writer.Flush();
                        return encoding.GetString(ms.ToArray());
                    }
                }

            // default to JSON
            default:
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions()
                    {
                        SkipValidation = true,
                        Indented = pretty,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    }))
                    {
                        System.Text.Json.JsonSerializer.Serialize(writer, obj, typeof(T));
                        writer.Flush();
                        return encoding.GetString(ms.ToArray());
                    }
                }
        }
    }
}
