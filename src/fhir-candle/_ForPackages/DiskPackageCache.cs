using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FhirCandle.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace fhir.candle._ForPackages;

public class DiskPackageCache
{
    public DiskPackageCache(string? rootDirectory = null)
    {
        Root = rootDirectory ?? GetDefaultRoot();
    }

    /// <summary>The legacy operator-selected cache root.</summary>
    public string Root { get; }

    internal static string GetDefaultRoot()
    {
        string? home = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("UserProfile")
            : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
                ? Environment.GetEnvironmentVariable("HOME")
                : null;
        if (home is null)
        {
            throw new Exception("Cannot determine rootpath of operating system");
        }
        return Path.Combine(home, ".fhir", "packages");
    }

    /// <summary>Gets the scope-independent legacy content path without native cache validation.</summary>
    /// <param name="reference">The requested package identity.</param>
    /// <returns>The package content directory.</returns>
    public string PackageContentFolder(PackageReference reference) =>
        Path.Combine(Root, reference.Name + "#" + reference.Version, "package");

    /// <summary>Checks only for the legacy content directory, including missing or corrupt manifests.</summary>
    /// <param name="reference">The requested package identity.</param>
    /// <returns>Whether the content directory exists.</returns>
    public Task<bool> IsInstalled(PackageReference reference) =>
        Task.FromResult(Directory.Exists(PackageContentFolder(reference)));

    /// <summary>Imports with the native installer and publishes the distributed tree at the requested legacy identity.</summary>
    /// <param name="reference">The requested identity, which need not match the concrete manifest version.</param>
    /// <param name="data">The downloaded archive.</param>
    /// <returns>The installation operation.</returns>
    public async Task Install(PackageReference reference, byte[] data)
    {
        Directory.CreateDirectory(Root);
        string operationRoot = Path.Combine(Root, ".candle-package-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationRoot);
        try
        {
            string distributedRoot;
            using (FhirPkg.Cache.DiskPackageCache nativeCache = new(
                operationRoot, NullLogger<FhirPkg.Cache.DiskPackageCache>.Instance))
            using (MemoryStream stream = new(data, writable: false))
            {
                FhirPkg.Models.PackageRecord installed = await nativeCache.ImportAsync(
                    stream, new FhirPkg.Cache.InstallCacheOptions { Unmodified = true });
                _ = nativeCache.GetPackageContentPath(installed.Reference)
                    ?? throw new InvalidDataException("Imported package content is not readable.");
                distributedRoot = installed.DirectoryPath;
            }

            // Only the identity's distributed tree is promoted. Native root metadata,
            // journals and locks remain in this operation's disposable cache.
            string content = PackageContentFolder(reference);
            string target = Path.GetDirectoryName(content)!;
            Directory.CreateDirectory(target);
            if (Directory.Exists(content))
            {
                Directory.Delete(content, recursive: true);
            }
            CopyDistributedTree(distributedRoot, target);
        }
        finally
        {
            Directory.Delete(operationRoot, recursive: true);
        }
    }

    private static void CopyDistributedTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDistributedTree(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static readonly JsonSerializerSettings SETTINGS = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        //Converters = new List<JsonConverter> { new AuthorJsonConverter(), new ManifestDateJsonConverter() }
    };

    public Task<PackageManifest?> ReadManifestEx(PackageReference reference)
    {
        string folder = PackageContentFolder(reference);

        if (!Directory.Exists(folder))
        {
            return Task.FromResult<PackageManifest?>(null);
        }

        string path = Path.Combine(folder, "package.json");
        if (!File.Exists(path))
        {
            return Task.FromResult<PackageManifest?>(null);
        }

        string json = File.ReadAllText(path);
        PackageManifest? manifest = JsonConvert.DeserializeObject<PackageManifest>(json, SETTINGS);

        return Task.FromResult(manifest);
    }


    /// <summary>
    /// Deletes a package from the disk package cache.
    /// </summary>
    /// <param name="reference">The package reference.</param>
    public Task Delete(PackageReference reference)
    {
        string target = PackageContentFolder(reference);
        if (!Directory.Exists(target))
        {
            return Task.CompletedTask;
        }

        // delete the directory recursively
        Directory.Delete(target, true);
        return Task.CompletedTask;
    }

}
