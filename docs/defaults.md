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
| .NET SDK                     | 10.0.403 |
| Azure.Messaging.ServiceBus   | 7.21.0   |
| Azure.Identity               | 1.21.0   |
| Microsoft.Extensions.Hosting | 10.0.14  |

Local container images, pinned by digest in [local/compose.yaml](../local/compose.yaml):

| Image                                                 | Pulled from tag | Digest                                                                    |
|-------------------------------------------------------|-----------------|---------------------------------------------------------------------------|
| mcr.microsoft.com/azure-messaging/servicebus-emulator | 2.0.0           | sha256:a00c9626c8960f6b9be6178aa91a7ac8f1a102c0d9deda7603a1ba0ac9d9ab51   |
| mcr.microsoft.com/mssql/server                        | 2022-latest     | sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718   |

## Queue properties

The same values apply to all five queues (`jit`, `r2r`, `aot`, `jit-chiseled`, `done`), in the emulator and in Azure.
They are chosen for this experiment, not platform defaults, and are set in
[local/emulator-config.json](../local/emulator-config.json).

| Property                              | Value  | Note                                                          |
|---------------------------------------|--------|---------------------------------------------------------------|
| `MaxDeliveryCount`                    | 10     |                                                               |
| `LockDuration`                        | PT1M   |                                                               |
| `DefaultMessageTimeToLive`            | PT1H   | The emulator's documented limit is 1 hour                     |
| `DeadLetteringOnMessageExpiration`    | false  |                                                               |
| `RequiresSession`                     | false  |                                                               |
| `RequiresDuplicateDetection`          | false  |                                                               |
| `DuplicateDetectionHistoryTimeWindow` | PT20S  | Has no effect while duplicate detection is off                |
| `ForwardTo`                           | empty  |                                                               |
| `ForwardDeadLetteredMessagesTo`       | empty  |                                                               |

## Facts to verify

Rows marked docs are written up, with links and dates, in [platform-facts.md](platform-facts.md).

| What                                               | Expected                                         | Actual                                | Source     |
|----------------------------------------------------|--------------------------------------------------|---------------------------------------|------------|
| Container Apps KEDA polling interval               | 30 s                                             | 30 s                                  | docs       |
| Container Apps cooldown period                     | 300 s                                            | 300 s                                 | docs       |
| Polling interval and cooldown configurable?        | Yes, in recent API versions                      | Yes, GA since 2025-01-01              | docs       |
| KEDA Service Bus scaler `messageCount` default     | 5 (scale to 1 replica needs ≥ 1)                 | 5; activation at more than 0 messages | docs       |
| Managed identity supported for the scale rule      | Yes                                              | Yes; the role needed isn't documented | docs       |
| Consumption free grant per subscription per month  | 180k vCPU-s, 360k GiB-s, 2M requests             |                                       | UNVERIFIED |
| Idle rate for a replica with min replicas 1        | Lower than the active rate                       |                                       | UNVERIFIED |
| `Azure.Messaging.ServiceBus` works with Native AOT | Unknown. Build with trimming and AOT warnings on | No warnings, starts; receive untested | runtime    |
