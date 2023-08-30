﻿// <copyright file="Utils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;
using System.Xml.Serialization;
using System.Xml;
using System.Net;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.Utility;
using System.Diagnostics;

namespace FhirCandle.Serialization;

/// <summary>Serialization utilties.</summary>
public static class Utils
{
    /// <summary>The JSON parser.</summary>
    private static FhirJsonPocoDeserializer _jsonParser = new(new FhirJsonPocoDeserializerSettings()
    {
        DisableBase64Decoding = false,
    });

    /// <summary>The JSON serializer for full resources.</summary>
    private static FhirJsonPocoSerializer _jsonSerializerFull = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = null,
    });

    /// <summary>The JSON serializer for summary=data.</summary>
    private static FhirJsonPocoSerializer _jsonSerializerData = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForText(),
    });

    /// <summary>The JSON serializer for summary=text.</summary>
    private static FhirJsonPocoSerializer _jsonSerializerText = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForData(),
    });

    /// <summary>The JSON serializer for summary=true.</summary>
    private static FhirJsonPocoSerializer _jsonSerializerSummary = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForSummary(),
    });

    /// <summary>The XML parser.</summary>
    private static FhirXmlPocoDeserializer _xmlParser = new(new FhirXmlPocoDeserializerSettings()
    {
        DisableBase64Decoding = false,
    });

    /// <summary>The XML serializer.</summary>
    private static FhirXmlPocoSerializer _xmlSerializer = new();

    /// <summary>Builds outcome for request.</summary>
    /// <param name="sc">       The screen.</param>
    /// <param name="message">  (Optional) The message.</param>
    /// <param name="issueType">(Optional) Type of the issue.</param>
    /// <returns>An OperationOutcome.</returns>
    public static OperationOutcome BuildOutcomeForRequest(
        HttpStatusCode sc, 
        string message = "",
        OperationOutcome.IssueType? issueType = null)
    {
        if (sc.IsSuccessful())
        {
            return new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = issueType ?? OperationOutcome.IssueType.Success,
                        Diagnostics = string.IsNullOrEmpty(message)
                            ? "Request processed successfully"
                            : message,
                    },
                },
            };
        }

        if (sc == HttpStatusCode.NotFound)
        {
            return new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = issueType ?? OperationOutcome.IssueType.NotFound,
                        Diagnostics = string.IsNullOrEmpty(message)
                            ? $"Not found: {sc.ToString()}"
                            : message,
                    },
                },
            };
        }

        return new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>()
            {
                new OperationOutcome.IssueComponent()
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = issueType ?? OperationOutcome.IssueType.Exception,
                    Diagnostics = string.IsNullOrEmpty(message)
                        ? $"Request failed with status code {sc.ToString()}"
                        : message,
                },
            },
        };
    }

    /// <summary>Try deserialize FHIR.</summary>
    /// <typeparam name="TResource">Type of the resource.</typeparam>
    /// <param name="content"> The content.</param>
    /// <param name="format">  Describes the format to use.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>A HttpStatusCode.</returns>
    public static HttpStatusCode TryDeserializeFhir<TResource>(
        string content,
        string format,
        out TResource? resource,
        out string exMessage)
        where TResource : Resource
    {
        //string utf8Content = null!;
        string[] formatComponents = format.Split(';', StringSplitOptions.TrimEntries);

        //IEnumerable<string> csComponents = formatComponents.Where(c => c.StartsWith("charset=", StringComparison.Ordinal) || c.StartsWith("charset ", StringComparison.Ordinal));
        //if (csComponents.Any())
        //{
        //    string[] charsetComponents = csComponents.First().Split('=', StringSplitOptions.TrimEntries);
        //    if (charsetComponents.Length == 2)
        //    {
        //        switch (charsetComponents[1])
        //        {
        //            case "utf-8":
        //                utf8Content = content;
        //                break;

        //            default:
        //                byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(content);
        //                utf8Content = System.Text.Encoding.UTF8.GetString(utf8Bytes);
        //                break;
        //        }
        //    }
        //}

        //if (utf8Content == null)
        //{
        //    utf8Content = content;
        //}

        switch (formatComponents[0])
        {
            case "json":
            case "fhir+json":
            case "application/json":
            case "application/fhir+json":
                try
                {
                    Resource parsed = _jsonParser.DeserializeResource(content);
                    if (parsed is TResource)
                    {
                        resource = (TResource)parsed;
                        exMessage = string.Empty;
                        return HttpStatusCode.OK;
                    }
                    else
                    {
                        resource = null;
                        exMessage = string.Empty;
                        return HttpStatusCode.UnprocessableEntity;
                    }
                }
                catch (Exception ex)
                {
                    resource = null;
                    exMessage = ex.Message;
                    return HttpStatusCode.UnprocessableEntity;
                }

            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                try
                {
                    Resource parsed = _xmlParser.DeserializeResource(content);
                    if (parsed is TResource)
                    {
                        resource = (TResource)parsed;
                        exMessage = string.Empty;
                        return HttpStatusCode.OK;
                    }
                    else
                    {
                        resource = null;
                        exMessage = string.Empty;
                        return HttpStatusCode.UnprocessableEntity;
                    }
                }
                catch (Exception ex)
                {
                    resource = null;
                    exMessage = ex.Message;
                    return HttpStatusCode.UnprocessableEntity;
                }

            default:
                {
                    // try and see if this has JSON contents, e.g., text/plain but has FHIR JSON
                    HttpStatusCode sc = TryDeserializeFhir(
                        content,
                        "application/fhir+json",
                        out resource,
                        out exMessage);

                    if (sc == HttpStatusCode.OK)
                    {
                        return sc;
                    }

                    // next, try and see if this has XML contents, e.g., text/plain but has FHIR XML
                    sc = TryDeserializeFhir(
                        content,
                        "application/fhir+xml",
                        out resource,
                        out exMessage);

                    if (sc == HttpStatusCode.OK)
                    {
                        return sc;
                    }

                    // still here means we don't know what this is
                    resource = null;
                    exMessage = string.Empty;
                    return HttpStatusCode.UnsupportedMediaType;
                }
        }
    }

    /// <summary>Serialize this object to the proper format.</summary>
    /// <param name="instance">   The instance.</param>
    /// <param name="format"> Destination format.</param>
    /// <param name="pretty">     If the output should be 'pretty' formatted.</param>
    /// <param name="summaryType">(Optional) Type of the summary.</param>
    /// <returns>A string.</returns>
    public static string SerializeFhir(
        Resource instance,
        string format,
        bool pretty,
        string summaryFlag = "")
    {
        // TODO: Need to add support for count

        string[] formatComponents = format.Split(';', StringSplitOptions.TrimEntries);

        System.Text.Encoding encoding = System.Text.Encoding.UTF8;
        //IEnumerable<string> csComponents = formatComponents.Where(c => c.StartsWith("charset=", StringComparison.Ordinal) || c.StartsWith("charset ", StringComparison.Ordinal));
        //if (csComponents.Any())
        //{
        //    string[] charsetComponents = csComponents.First().Split('=', StringSplitOptions.TrimEntries);
        //    if (charsetComponents.Length == 2)
        //    {
        //        switch (charsetComponents[1])
        //        {
        //            case "utf-8":
        //                encoding = System.Text.Encoding.UTF8;
        //                break;

        //            case "utf-16":
        //                encoding = System.Text.Encoding.Unicode;
        //                break;

        //            case "utf-32":
        //                encoding = System.Text.Encoding.UTF32;
        //                break;

        //            case "ascii":
        //                encoding = System.Text.Encoding.ASCII;
        //                break;
        //        }
        //    }
        //}

        switch (formatComponents[0])
        {
            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                {
                    SerializationFilter? serializationFilter;

                    switch (summaryFlag.ToLowerInvariant())
                    {
                        case "":
                        case "false":
                        default:
                            serializationFilter = null;
                            break;

                        case "true":
                            serializationFilter = SerializationFilter.ForSummary();
                            break;

                        case "text":
                            serializationFilter = SerializationFilter.ForText();
                            break;

                        case "data":
                            serializationFilter = SerializationFilter.ForData();
                            break;
                    }

                    if (pretty || (encoding != System.Text.Encoding.UTF8))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        using (System.Xml.XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings() { Encoding = encoding, Indent = pretty }))
                        {
                            _xmlSerializer.Serialize(instance, writer, serializationFilter);
                            writer.Flush();
                            return encoding.GetString(ms.ToArray());
                        }
                    }

                    return _xmlSerializer.SerializeToString(instance, serializationFilter);
                }

            // default to JSON
            default:
                {
                    switch (summaryFlag.ToLowerInvariant())
                    {
                        case "":
                        case "false":
                        default:
                            if (pretty || (encoding != System.Text.Encoding.UTF8))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions()
                                {
                                    SkipValidation = true,
                                    Indented = pretty,
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                }))
                                {
                                    _jsonSerializerFull.Serialize(instance, writer);
                                    writer.Flush();
                                    return encoding.GetString(ms.ToArray());
                                }
                            }

                            return _jsonSerializerFull.SerializeToString(instance);

                        case "true":
                            if (pretty || (encoding != System.Text.Encoding.UTF8))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions()
                                {
                                    SkipValidation = true,
                                    Indented = pretty,
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                }))
                                {
                                    _jsonSerializerSummary.Serialize(instance, writer);
                                    writer.Flush();
                                    return encoding.GetString(ms.ToArray());
                                }
                            }

                            return _jsonSerializerSummary.SerializeToString(instance);

                        case "text":
                            if (pretty || (encoding != System.Text.Encoding.UTF8))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions()
                                {
                                    SkipValidation = true,
                                    Indented = pretty,
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                }))
                                {
                                    _jsonSerializerText.Serialize(instance, writer);
                                    writer.Flush();
                                    return encoding.GetString(ms.ToArray());
                                }
                            }

                            return _jsonSerializerText.SerializeToString(instance);

                        case "data":
                            if (pretty || (encoding != System.Text.Encoding.UTF8))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                using (Utf8JsonWriter writer = new Utf8JsonWriter(ms, new JsonWriterOptions()
                                {
                                    SkipValidation = true,
                                    Indented = pretty,
                                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                }))
                                {
                                    _jsonSerializerData.Serialize(instance, writer);
                                    writer.Flush();
                                    return encoding.GetString(ms.ToArray());
                                }
                            }

                            return _jsonSerializerData.SerializeToString(instance);
                    }
                }
                //return instance.ToJson(_jsonSerializerSettings);
        }
    }
}
