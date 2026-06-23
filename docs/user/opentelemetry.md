# Using OpenTelemetry

OpenTelemetry instrumentation can be enabled via either the
`--otel-otlp-endpoint` argument or the `OTEL_EXPORTER_OTLP_ENDPOINT` environment
variable. For example, to send traces to a Jaeger instance running on
`localhost:4317`:

```
fhir-candle -o --otel-otlp-endpoint http://localhost:4317
```

For local testing, you can run a Jaeger instance in Docker with the following
command:

```
docker run --rm --name jaeger -p 4317:4317 -p 4318:4318 -p 5778:5778 -p 16686:16686 -p 14250:14250 jaegertracing/all-in-one:latest
```

## Related

- [Getting started](getting-started.md)
