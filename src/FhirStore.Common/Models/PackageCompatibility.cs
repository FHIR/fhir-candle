// <copyright file="PackageCompatibility.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>A package identity with legacy, scope-independent name and version equality.</summary>
/// <remarks>Parsing and resolution belong to the host. Scope does not form part of the moniker.</remarks>
public struct PackageReference
{
    /// <summary>The optional bare package scope.</summary>
    public string? Scope;

    /// <summary>The unscoped package name, or null for an empty reference.</summary>
    public string? Name;

    /// <summary>The requested version or tag, which may be null or empty.</summary>
    public string? Version;

    /// <summary>Creates an unscoped reference without parsing its components.</summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The requested version or tag.</param>
    public PackageReference(string name, string? version)
        : this(null, name, version)
    {
    }

    /// <summary>Creates a reference without normalizing its components.</summary>
    /// <param name="scope">The optional bare scope.</param>
    /// <param name="name">The unscoped package name.</param>
    /// <param name="version">The requested version or tag.</param>
    public PackageReference(string? scope, string name, string? version)
    {
        Scope = scope;
        Name = name;
        Version = version;
    }

    /// <summary>Gets the scope-independent name and version separated by an at sign.</summary>
    public readonly string Moniker => $"{Name}@{Version}";

    /// <summary>Gets an empty reference.</summary>
    public static PackageReference None => default;

    /// <summary>Gets whether both the name and version are non-null, including empty strings.</summary>
    public readonly bool Found => Name is not null && Version is not null;

    /// <summary>Gets whether the name or version is null.</summary>
    public readonly bool NotFound => !Found;

    /// <summary>Returns the moniker with the legacy not-found marker when a component is null.</summary>
    /// <returns>The display identity.</returns>
    public override readonly string ToString() => Moniker + (Found ? string.Empty : " (NOT FOUND)");

    /// <summary>Deconstructs the reference without its scope.</summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The requested version or tag.</param>
    public readonly void Deconstruct(out string? name, out string? version)
    {
        name = Name;
        version = Version;
    }

    /// <summary>Compares names and versions case-sensitively, ignoring scope.</summary>
    /// <param name="obj">The value to compare.</param>
    /// <returns>True for an equal package identity.</returns>
    public override readonly bool Equals(object? obj) => obj is PackageReference reference && this == reference;

    /// <summary>Returns the legacy name/version tuple hash, excluding scope.</summary>
    /// <returns>The identity hash.</returns>
    public override readonly int GetHashCode() => (Name, Version).GetHashCode();

    /// <summary>Compares names and versions case-sensitively, ignoring scope.</summary>
    /// <param name="left">The first reference.</param>
    /// <param name="right">The second reference.</param>
    /// <returns>True for equal identities.</returns>
    public static bool operator ==(PackageReference left, PackageReference right) =>
        left.Name == right.Name && left.Version == right.Version;

    /// <summary>Compares names and versions case-sensitively, ignoring scope.</summary>
    /// <param name="left">The first reference.</param>
    /// <param name="right">The second reference.</param>
    /// <returns>True for different identities.</returns>
    public static bool operator !=(PackageReference left, PackageReference right) => !(left == right);
}

/// <summary>The consumed catalog description of a package.</summary>
public class PackageCatalogEntry
{
    /// <summary>Gets or sets the package name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the description, including CI repository literals.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the advertised FHIR version.</summary>
    public string? FhirVersion { get; set; }
}

/// <summary>A package listing with its advertised releases and tag identities.</summary>
public class PackageListing
{
    /// <summary>Gets or sets the listing identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the listing name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the tag-to-version dictionary, including CI branch tags.</summary>
    public Dictionary<string, string>? DistTags { get; set; }

    /// <summary>Gets or sets the version-to-release dictionary.</summary>
    public Dictionary<string, PackageRelease>? Versions { get; set; }
}

/// <summary>The consumed information about an advertised package release.</summary>
public class PackageRelease
{
    /// <summary>Gets or sets the package name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the advertised version.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the release description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the advertised FHIR version.</summary>
    public string? FhirVersion { get; set; }

    /// <summary>Gets or sets the publication URL.</summary>
    public string? Url { get; set; }

    /// <summary>Gets or sets the archive distribution information.</summary>
    public Dist? Dist { get; set; }
}

/// <summary>The consumed archive distribution information.</summary>
public class Dist
{
    /// <summary>Gets or sets the archive URL.</summary>
    public string? Tarball { get; set; }
}

/// <summary>A data-only version projection; the host owns parsing, filtering, and ordering.</summary>
public class Versions
{
    /// <summary>Creates an empty projection.</summary>
    public Versions()
        : this([])
    {
    }

    /// <summary>Copies an already projected sequence without parsing or sorting it.</summary>
    /// <param name="items">The projected version strings in host-selected order.</param>
    public Versions(IEnumerable<string> items)
    {
        Items = items.ToArray();
    }

    /// <summary>Gets the projected versions.</summary>
    public IReadOnlyCollection<string> Items { get; }

    /// <summary>Gets whether the projection contains no versions.</summary>
    public bool IsEmpty => Items.Count == 0;
}
