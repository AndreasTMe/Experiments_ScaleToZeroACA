# Defaults in play

The platform and library values this experiment depends on, and how each was checked.

How each value was checked:

- **probe**: read at runtime from an object built with the installed package.
- **runtime**: read back from the running service or the live Azure resource.
- **source**: read in the package source at the release tag, or in the package's XML docs.
- **docs**: read in current Microsoft or KEDA documentation; the link and date are in the Source column.
- **UNVERIFIED**: taken from the plan or from memory and not checked yet.

## Versions

| Component                    | Version  |
|------------------------------|----------|
| .NET SDK                     | 10.0.114 |
| Azure.Messaging.ServiceBus   | 7.21.0   |
| Azure.Identity               | 1.21.0   |
| Microsoft.Extensions.Hosting | 10.0.14  |

## Facts to verify

| What                                               | Expected                                         | Actual | Source     |
|----------------------------------------------------|--------------------------------------------------|--------|------------|
| Container Apps KEDA polling interval               | 30 s                                             |        | UNVERIFIED |
| Container Apps cooldown period                     | 300 s                                            |        | UNVERIFIED |
| Polling interval and cooldown configurable?        | Yes, in recent API versions                      |        | UNVERIFIED |
| KEDA Service Bus scaler `messageCount` default     | 5 (scale to 1 replica needs ≥ 1)                 |        | UNVERIFIED |
| Managed identity supported for the scale rule      | Yes                                              |        | UNVERIFIED |
| Consumption free grant per subscription per month  | 180k vCPU-s, 360k GiB-s, 2M requests             |        | UNVERIFIED |
| Idle rate for a replica with min replicas 1        | Lower than the active rate                       |        | UNVERIFIED |
| `Azure.Messaging.ServiceBus` works with Native AOT | Unknown. Build with trimming and AOT warnings on |        | UNVERIFIED |
