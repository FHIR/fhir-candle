using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FhirCandle.Configuration;
using FhirCandle.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace fhir.candle._ForPackages;

public class DiskPackageCache
{
    private readonly FhirPkg.PackageInstallLimits _limits;

    public DiskPackageCache(string? rootDirectory = null) : this(rootDirectory, CreateInstallLimits(new CandleConfig()))
    {
    }

    internal DiskPackageCache(string? rootDirectory, FhirPkg.PackageInstallLimits limits)
    {
        Root = rootDirectory ?? GetDefaultRoot();
        _limits = limits;
    }

    internal static FhirPkg.PackageInstallLimits CreateInstallLimits(CandleConfig config)
    {
        FhirPkg.PackageInstallLimits limits = new()
        {
            MaxCompressedBytes = config.PackageMaxCompressedBytes,
            MaxExpandedBytes = config.PackageMaxExpandedBytes,
            MaxEntryBytes = config.PackageMaxEntryBytes,
            MaxArchiveEntries = config.PackageMaxArchiveEntries,
            MaxArchivePathLength = config.PackageMaxArchivePathLength,
            MaxArchiveDepth = config.PackageMaxArchiveDepth,
        };
        limits.Validate();
        return limits;
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

    internal bool IsInstalledForLatest(PackageReference reference)
    {
        string content = PackageContentFolder(reference);
        return ValidateLatestIdentity(Path.GetDirectoryName(content)!) && Directory.Exists(content);
    }

    private static bool ValidateLatestIdentity(string identity)
    {
        string key = Path.GetFileName(identity);
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(identity);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new LatestCacheIdentityException($"Unable to inspect automatic package cache key '{key}' at '{identity}'.", exception);
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new LatestCacheIdentityException($"Cannot verify automatic package cache key '{key}': '{identity}' is not a directory.");
        }

        // A successful path lookup may have used a different spelling. Inspect
        // the stored leaves rather than inferring a match from the requested path.
        string? alias = null;
        bool multipleAliases = false;
        try
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(identity))!;
            foreach (string entry in Directory.EnumerateFileSystemEntries(parent, "*",
                new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0 }))
            {
                string leaf = Path.GetFileName(entry);
                if (string.Equals(key, leaf, StringComparison.Ordinal))
                {
                    return true;
                }
                if (string.Equals(key, leaf, StringComparison.OrdinalIgnoreCase))
                {
                    multipleAliases = alias is not null;
                    alias = leaf;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new LatestCacheIdentityException($"Unable to enumerate the parent of automatic package cache key '{key}' at '{identity}'.", exception);
        }

        if (alias is null || multipleAliases)
        {
            throw new LatestCacheIdentityException($"Cannot verify a unique stored leaf for automatic package cache key '{key}' at '{identity}'.");
        }

        int versionStart = key.LastIndexOf('#') + 1;
        if (!key.AsSpan(versionStart).SequenceEqual(alias.AsSpan(versionStart)))
        {
            throw new LatestCacheIdentityException(
                $"Automatic package cache key '{key}' resolves to stored key '{alias}' with a different version spelling. " +
                "Use a separate cache root or arrange operator-managed relocation of the stored key.");
        }

        return true;
    }

    /// <summary>Imports with the native installer and publishes the distributed tree at the requested legacy identity.</summary>
    /// <param name="reference">The requested identity, which need not match the concrete manifest version.</param>
    /// <param name="data">The downloaded archive.</param>
    /// <returns>The installation operation.</returns>
    public Task Install(PackageReference reference, byte[] data) =>
        Install(reference, data, validateLatestIdentity: false);

    internal async Task Install(PackageReference reference, byte[] data, bool validateLatestIdentity)
    {
        Directory.CreateDirectory(Root);
        string operationRoot = Path.Combine(Root, ".candle-package-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationRoot);
        try
        {
            string distributedRoot;
            using (FhirPkg.Cache.DiskPackageCache nativeCache = new(
                operationRoot, NullLogger<FhirPkg.Cache.DiskPackageCache>.Instance,
                timeProvider: null, new FhirPkg.FileSystemRetryOptions(), _limits))
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
            if (validateLatestIdentity)
            {
                ValidateLatestIdentity(target);
            }
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

internal sealed class LatestCacheIdentityException(string message, Exception? innerException = null)
    : IOException(message, innerException)
{
}
