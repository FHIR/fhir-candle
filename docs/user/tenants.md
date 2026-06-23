# FHIR tenants

By default, this software loads three FHIR 'tenants':

- a FHIR R4 endpoint at `/r4`,
- a FHIR R4B endpoint at `/r4b`, and
- a FHIR R5 endpoint at `/r5`.

The tenants can be controlled by command line arguments — note that manually
specifying any tenants overrides the default configuration and will *only* load
the ones specified. To load only an R4 endpoint at `fhir`, the arguments would
include `--r4 fhir`. You can specify multiple tenants for the same version; for
example, `--r5 fhir --r5 also-fhir` will create two endpoints.

## Related

- [Getting started](getting-started.md)
- [Loading initial data](loading-data.md)
