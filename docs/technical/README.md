# Technical documentation

Architecture and contribution references for people **building** fhir-candle.
If you only want to run or call the server, see the [user docs](../user/).

## Pages

- [Architecture](architecture.md) — the shared store projects, version-specific UI,
  and the project dependency graph.
- [Assembly aliases](assembly-aliases.md) — the `candleR4` / `candleR4B` /
  `candleR5` extern aliases and why they exist.
- [Operations and hooks](operations-and-hooks.md) — `IFhirOperation` /
  `IFhirInteractionHook` reflection-based plugin discovery and conventions.
- [Storage model](storage-model.md) — `IFhirStore`, `IResourceStore`,
  `FhirRequestContext` / `FhirResponseContext`, and multi-tenant hosting.
- [Building and testing](building-and-testing.md) — build/test commands,
  multi-targeting, and test organization.
- [Firely SDK compatibility](sdk6-compatibility.md) — current dependencies,
  historical migration evidence, and the browser checklist.
- [Fluent UI integration](fluent-ui-integration.md) — theme preferences,
  component registration, and packaged assets.
- [Performance comparison](performance-comparison.md) — measured workloads,
  artifact identities, and the limits of the results.
- [Releasing](releasing.md) — tagged releases, immutable tool qualification,
  NuGet trusted publishing, container tags, and deployment gates.
- [Contributing notes](contributing-notes.md) — `.editorconfig` style,
  copyright header, namespace naming, and null-handling conventions.
- [`$validate` implementation note](validate-implementation.md) — implementation
  approach and the follow-up path to profile/terminology validation.
- [October 2026 dependency refresh](dependency-refresh-2026-10/README.md) —
  historical package decisions and Windows results, not today's package pins.
