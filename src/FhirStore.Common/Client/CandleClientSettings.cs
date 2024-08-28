// <copyright file="CandleClientSettings.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using System.Net;
using FhirCandle.Extensions;

namespace FhirCandle.Client;

/// <summary>
/// A candle client settings - mostly passthrough options to the Firely SDK Client.</summary>
public record class CandleClientSettings : ICloneable
{
    /// <summary>Values that represent resource formats.</summary>
    public enum ResourceFormatCodes : int
    {
        [FhirLiteral("application/fhir+xml")]
        Xml = 1,

        [FhirLiteral("application/fhir+json")]
        Json = 2,

        [FhirLiteral("")]
        Unknown = 3,
    }

    /// <summary>Values that represent return preferences.</summary>
    public enum ReturnPreferenceCodes
    {
        /// <summary>
        /// Prefer to receive the full resource in the body after completion of the interaction
        /// </summary>
        [FhirLiteral("representation")]
        Representation,

        /// <summary>
        /// Prefer to not a receive a body after completion of the interaction
        /// </summary>
        [FhirLiteral("minimal")]
        Minimal,

        /// <summary>
        /// Prefer to receive an OperationOutcome resource containing hints and warnings about the 
        /// operation rather than the full resource
        /// </summary>
        [FhirLiteral("OperationOutcome")]
        OperationOutcome,

        /// <summary>
        /// Prefer to run the operation as an asynchronous request
        /// (http://hl7.org/fhir/r4/async.html)
        /// - This may also be applicable in prior versions (though not part of that stamdard)
        /// </summary>
        [FhirLiteral("respond-async")]
        RespondAsync,
    }

    /// <summary>Values that represent search parameter handlings.</summary>
    public enum SearchParameterHandling
    {
        /// <summary>
        /// Server should return an error for any unknown or unsupported parameter        
        /// </summary>
        [FhirLiteral("strict")]
        Strict,

        /// <summary>
        /// Server should ignore any unknown or unsupported parameter
        /// </summary>
        [FhirLiteral("lenient")]
        Lenient
    }

    /// <summary>
    /// Whether or not to ask the server for a CapabilityStatement and verify FHIR version compatibility before
    /// issuing requests to the server.
    /// </summary>
    public bool VerifyFhirVersion { get; init; } = false;

    /// <summary>
    /// Normally, the FhirClient will derive the FHIR version (e.g. 4.0.3) the client is communicating with
    /// from the metadata of the assembly containing the resource POCOs. Use this member to override this version.
    /// </summary>
    public string? ExplicitFhirVersion { get; init; } = null;

    /// <summary>
    /// The preferred format of the content to be used when communicating with the FHIR server (JSON or XML)
    /// </summary>
    public ResourceFormatCodes PreferredFormat { get; init; } = ResourceFormatCodes.Json;

    /// <summary>
    /// When passing the content preference, use the _format parameter instead of the request header
    /// </summary>
    public bool UseFormatParameter { get; init; } = false;

    /// <summary>
    /// When <see langword="true"/> the MIME-type parameter fhirVersion will be added the Accept header. This is necessary 
    /// when the FHIR server supports multiple FHIR versions.
    /// </summary>
    public bool UseFhirVersionInAcceptHeader { get; init; } = false;

    /// <summary>
    /// The timeout (in milliseconds) to be used when making calls to the FHIR server
    /// </summary>
    public int Timeout { get; init; } = 100 * 1000;

    /// <summary>
    /// Should calls to Create, Update and transaction operations return the whole updated content, 
    /// minimal content or an OperationOutcome (see https://hl7.org/fhir/http.html#return).
    /// </summary>
    /// <remarks>When null, no Prefer header with a "return=" prefix will be sent.</remarks>
    public ReturnPreferenceCodes? ReturnPreference { get; init; } = null;

    /// <summary>
    /// Request the server to use the asynchronous request pattern (https://hl7.org/fhir/async.html).
    /// </summary>
    public bool UseAsync { get; init; } = false;

    /// <summary>
    /// Should server return which search parameters were supported after executing a search?
    /// </summary>
    /// <remarks>If set to null, no Prefer header with a "handling=" prefix will be sent.</remarks>
    public SearchParameterHandling? PreferredParameterHandling { get; init; } = null;

    /// <summary>
    /// This will do 2 things:
    /// 1. Add the header Accept-Encoding: gzip, deflate
    /// 2. decompress any responses that have Content-Encoding: gzip (or deflate)
    /// </summary>
    public bool PreferCompressedResponses { get; init; } = false;

    /// <summary>
    /// Compress request bodies using the selected method. Note: only <see cref="DecompressionMethods.Deflate"/> and
    /// <see cref="DecompressionMethods.GZip"/> are currently supported.
    /// </summary>
    /// <remarks>If a server does not handle compressed requests using this method, it will return a 415 response.</remarks>
    public DecompressionMethods RequestBodyCompressionMethod { get; init; } = DecompressionMethods.None;

    /// <summary>Initializes a new instance of the <see cref="CandleClientSettings"/> class.</summary>
    public CandleClientSettings() { }

    /// <summary>Clone constructor. Generates a new <see cref="CandleClientSettings"/> instance initialized from the state of the specified instance.</summary>
    /// <exception cref="ArgumentNullException">The specified argument is <c>null</c>.</exception>
    public CandleClientSettings(CandleClientSettings other)
    {
        PreferCompressedResponses = other.PreferCompressedResponses;
        PreferredFormat = other.PreferredFormat;
        ReturnPreference = other.ReturnPreference;
        UseAsync = other.UseAsync;
        Timeout = other.Timeout;
        UseFormatParameter = other.UseFormatParameter;
        UseFhirVersionInAcceptHeader = other.UseFhirVersionInAcceptHeader;
        VerifyFhirVersion = other.VerifyFhirVersion;
        ExplicitFhirVersion = other.ExplicitFhirVersion;
        PreferredParameterHandling = other.PreferredParameterHandling;
        RequestBodyCompressionMethod = other.RequestBodyCompressionMethod;
    }

    /// <summary>Creates a new <see cref="CandleClientSettings"/> instance with default property values.</summary>
    public static CandleClientSettings CreateDefault() => new();

    /// <summary>Makes a deep copy of this object.</summary>
    /// <returns>A copy of this object.</returns>
    object ICloneable.Clone() => this with { };
}
