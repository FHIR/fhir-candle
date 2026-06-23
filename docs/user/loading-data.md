# Loading initial data

The server will load initial data specified by the `--fhir-source` argument. If
the path specified is a relative path, the software will look for the directory
starting at the current running path.

If the system is loading multiple tenants, it will check the path for additional
directories based on the tenant names. For example, a path like `fhirData`
passed into the default server will look for `fhirData/r4`, `fhirData/r4b`, and
`fhirData/r5`. If tenant directories are not found, all tenants will try to load
resources from the specified path.

## Related

- [FHIR tenants](tenants.md)
- [Getting started](getting-started.md)
