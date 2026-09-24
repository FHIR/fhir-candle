// <copyright file="LocalPackageFixture.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FhirCandle.Configuration;
using FhirCandle.Utils;

namespace fhir.candle.Tests.Extensions;

/// <summary>Owns only synthetic package/source directories and builds archives with the BCL.</summary>
internal sealed class LocalPackageFixture : IDisposable
{
    internal const string PackageVersion = "1.0.0";

    private bool _disposed;

    internal LocalPackageFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), $"fhir-candle-sdk6-package-{Guid.NewGuid():N}");
        CacheDirectory = CreateDirectory("cache");
        SourceDirectory = CreateDirectory("source");
    }

    internal string Root { get; }
    internal string CacheDirectory { get; }
    internal string SourceDirectory { get; }

    internal CandleConfig Configuration(params string[] registries) => new()
    {
        PublicUrl = "http://127.0.0.1",
        ListenPort = 0,
        FhirCacheDirectory = CacheDirectory,
        SourceDirectory = SourceDirectory,
        UseOfficialRegistries = false,
        AdditionalFhirRegistryUrls = registries,
        AdditionalNpmRegistryUrls = [],
        PublishedPackages = [],
        CiPackages = [],
        ReferenceImplementation = null,
        TenantsR4 = [],
        TenantsR4B = [],
        TenantsR5 = [],
        TenantsR6 = [],
        SmtpHost = string.Empty,
        SmtpUser = string.Empty,
        SmtpPassword = string.Empty,
        SmtpPort = 0,
        ZulipUrl = string.Empty,
        ZulipEmail = string.Empty,
        ZulipKey = string.Empty,
    };

    internal string CreateDirectory(string name)
    {
        RequireFileName(name);
        string path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    internal void WriteInitialResource(string tenant, string fileName, string contents)
    {
        RequireFileName(tenant);
        RequireFileName(fileName);
        string directory = Path.Combine(SourceDirectory, tenant);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), contents, new UTF8Encoding(false));
    }

    internal static string VersionName(FhirReleases.FhirSequenceCodes version) => version switch
    {
        FhirReleases.FhirSequenceCodes.R4 => "r4",
        FhirReleases.FhirSequenceCodes.R4B => "r4b",
        FhirReleases.FhirSequenceCodes.R5 => "r5",
        _ => throw new ArgumentOutOfRangeException(nameof(version)),
    };

    internal static string FhirVersion(FhirReleases.FhirSequenceCodes version) => version switch
    {
        FhirReleases.FhirSequenceCodes.R4 => "4.0.1",
        FhirReleases.FhirSequenceCodes.R4B => "4.3.0",
        FhirReleases.FhirSequenceCodes.R5 => "5.0.0",
        _ => throw new ArgumentOutOfRangeException(nameof(version)),
    };

    internal static string PackageName(FhirReleases.FhirSequenceCodes version) => "example.test.sdk6." + VersionName(version);

    internal static string Manifest(FhirReleases.FhirSequenceCodes version) =>
        CompatibilityReplay.LoadFixture(version, "package-manifest.json");

    internal static byte[] VersionArchive(FhirReleases.FhirSequenceCodes version) =>
        Archive(Manifest(version), new Dictionary<string, string>
        {
            ["Patient-wire-patient.json"] = WithId(CompatibilityReplay.LoadFixture(version, "patient.json"), "wire-patient"),
            ["Observation-package-observation.xml"] = WithId(CompatibilityReplay.LoadFixture(version, "observation.xml"), "package-observation"),
        });

    internal static byte[] Archive(string manifest, IReadOnlyDictionary<string, string> resources)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (TarWriter tar = new(gzip, TarEntryFormat.Ustar, leaveOpen: true))
        {
            tar.WriteEntry(new UstarTarEntry(TarEntryType.Directory, "package/"));
            WriteEntry("package.json", manifest);
            foreach (KeyValuePair<string, string> resource in resources)
            {
                RequireFileName(resource.Key);
                WriteEntry(resource.Key, resource.Value);
            }

            void WriteEntry(string fileName, string contents)
            {
                using MemoryStream data = new(Encoding.UTF8.GetBytes(contents));
                UstarTarEntry entry = new(TarEntryType.RegularFile, "package/" + fileName)
                {
                    DataStream = data,
                    ModificationTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                };
                tar.WriteEntry(entry);
            }
        }
        return output.ToArray();
    }

    internal static string WithId(string resource, string id)
    {
        if (resource.TrimStart().StartsWith('{'))
        {
            JsonObject json = JsonNode.Parse(resource)?.AsObject() ?? throw new InvalidOperationException("Missing JSON resource.");
            json["id"] = id;
            return json.ToJsonString();
        }

        XElement xml = XElement.Parse(resource, LoadOptions.PreserveWhitespace);
        XNamespace fhir = "http://hl7.org/fhir";
        xml.Element(fhir + "id")?.Remove();
        xml.AddFirst(new XElement(fhir + "id", new XAttribute("value", id)));
        return xml.ToString(SaveOptions.DisableFormatting);
    }

    private static void RequireFileName(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new ArgumentException("Use a single owned file or directory name.", nameof(name));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Directory.Delete(Root, recursive: true);
        _disposed = true;
    }
}
