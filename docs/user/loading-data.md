# Loading initial data

The server will load initial data specified by the `--fhir-source` argument. If
the path specified is a relative path, the software will look for the directory
starting at the current running path.

If the system is loading multiple tenants, it will check the path for additional
directories based on the tenant names. For example, a path like `fhirData`
passed into the default server will look for `fhirData/r4`, `fhirData/r4b`, and
`fhirData/r5`. If tenant directories are not found, all tenants will try to load
resources from the specified path.

## Automatic package version selection

When a package version is omitted, empty, or `latest`, Candle checks registry
listings in the package service's registration order:

1. Official FHIR registries, if enabled, in built-in order:
   `https://packages.fhir.org/`, then `https://packages2.fhir.org/packages/`.
2. Additional FHIR registries, in configured order.
3. Additional NPM registries, in configured order.

Candle selects the highest semantic precedence among eligible registry winners,
including eligible prereleases. When winners from different registries tie, it
keeps the first winner's full version, including any build metadata. This
replaces text ordering across registries. Listing and selection happen before
the cache check, even on a warm cache or a repeat call.

For a resolved package identity (name and version) not yet marked as processed,
Candle checks for a cache hit for that exact identity. On an ordinary miss, it
tries to download and install the selected exact version in the same registry
order. The archive need not come from the registry that listed the winning
version. If none of those attempts succeeds, installation fails without
falling back to a lower version.

Candle marks the resolved identity as processed before attempting acquisition.
After those archive attempts are exhausted, a later automatic request on the
same package service still queries the listings. If it resolves to the same
identity, Candle skips it: no new archive request, no returned package reference
for that directive, and no repeated archive-exhaustion error. This skip does not
bypass listing or resolution failures or carry over to a different identity.

For automatic selection only, Candle refuses to use or replace an existing
cache entry if the requested `Name#Version` key resolves to it with a different
version casing (for example, `10.0.0+abc` resolving to `10.0.0+ABC`). Candle
checks the key before accepting a cache hit or requesting an archive, and
again before publishing a downloaded archive. A later automatic directive can
also validate a package already included in the same batch's results without
reacquiring it. Use a separate cache root or deliberately relocate the
conflicting entry. This is not a general cache integrity check or protection
against concurrent writes.

## Package import limits

Package acquisition uses finite, Candle-owned limits. These intentionally narrow
the formerly permissive archive envelope; increase them for trusted packages
that need more room. All values are positive integers. Byte settings are `long`
(64-bit), in **bytes**, not MB strings; the other settings are 32-bit integers.

| CLI option | JSON key / unprefixed environment variable | `Candle`-prefixed environment variable | Default |
|---|---|---|---|
| `--package-max-compressed-bytes` | `Package_Max_Compressed_Bytes` | `CandlePackage_Max_Compressed_Bytes` | 104857600 bytes (100 MiB) |
| `--package-max-expanded-bytes` | `Package_Max_Expanded_Bytes` | `CandlePackage_Max_Expanded_Bytes` | 1073741824 bytes (1 GiB) |
| `--package-max-entry-bytes` | `Package_Max_Entry_Bytes` | `CandlePackage_Max_Entry_Bytes` | 134217728 bytes (128 MiB) |
| `--package-max-archive-entries` | `Package_Max_Archive_Entries` | `CandlePackage_Max_Archive_Entries` | 50000 entries |
| `--package-max-archive-path-length` | `Package_Max_Archive_Path_Length` | `CandlePackage_Max_Archive_Path_Length` | 1024 characters |
| `--package-max-archive-depth` | `Package_Max_Archive_Depth` | `CandlePackage_Max_Archive_Depth` | 32 components |

The default environment prefix concatenates `Candle` directly with the key
(no inserted underscore). CLI overrides external configuration, which overrides
defaults. Within the host's external providers, unprefixed environment values
override `Candle`-prefixed values, which override JSON values.

Compressed size uses the actual gzip archive bytes. Expanded size is aggregate
extracted file bytes; entry size is per expanded file. Directory entries count
toward the entry-count limit. Path length uses the longer of normalized canonical
and exact-spelling paths, not the cache's absolute filesystem path. Depth counts
all normalized components, **including `package` and the filename**: 30 nested
directories between them means depth 32.

Enabled-cache initialization validates and snapshots all six settings at the
first `FhirPackageService.Init()`, before readiness or cache-directory creation.
Later configuration mutation does not change that service's policy. Nonpositive
values, entry bytes greater than expanded bytes, or overflow in checked
`compressed + expanded` / `entry × archive entries` accounting fail explicitly
with native `PackageInstallException`, `InvalidPolicy` / `PolicyValidation`.
This is accounting validation, not a checksum configuration. Non-numeric or
out-of-range CLI values fail parsing; invalid external numeric values fail
configuration binding. An explicitly disabled cache skips unused policy
validation.

The limits cover native imports, including CI imports, not warm-cache directory
reads. They **do not cap HTTP buffering or download spooling/memory**: the host
still buffers downloads before importing. The registry factory receives the same
snapshot; its limits govern NPM publication preparation, not download spooling.
Archive structure and identity validation remain enabled when raising limits.
Published acquisition logs a sanitized limit error code and the matching CLI
option, then tries remaining registries; if none succeeds the normal generic
installation failure remains. CI limit failures retain their typed exception.

Candle explicitly supplies all six limits, ignoring `FHIRPKG_MAX_COMPRESSED_BYTES`,
`FHIRPKG_MAX_EXPANDED_BYTES`, `FHIRPKG_MAX_ENTRY_BYTES`,
`FHIRPKG_MAX_ARCHIVE_ENTRIES`, `FHIRPKG_MAX_ARCHIVE_PATH_LENGTH`, and
`FHIRPKG_MAX_ARCHIVE_DEPTH`. Existing ambient `FHIRPKG_FS_*` filesystem retry
settings are unchanged; there are no new Candle retry settings.

For example, this JSON configuration raises depth to 33 while retaining the
other limits:

```json
{
  "Package_Max_Compressed_Bytes": 104857600,
  "Package_Max_Expanded_Bytes": 1073741824,
  "Package_Max_Entry_Bytes": 134217728,
  "Package_Max_Archive_Entries": 50000,
  "Package_Max_Archive_Path_Length": 1024,
  "Package_Max_Archive_Depth": 33
}
```

Equivalent CLI override (allows `package` + 31 directories + filename):

```powershell
dotnet run --project src\fhir-candle\fhir-candle.csproj --framework net11.0 -- --package-max-archive-depth 33
```

## Related

- [FHIR tenants](tenants.md)
- [Getting started](getting-started.md)
