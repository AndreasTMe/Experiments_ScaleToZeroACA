# Platform facts

What the experiment relies on from Container Apps and KEDA, each with where it was read. All pages were read on
2026-11-01. "Page date" is the date in the page's own metadata.

## Sources

| Ref    | Page                                                                                                                                                                                                   | Page date    |
|--------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------|
| scale  | [Set scaling rules in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)                                                                                          | 2026-05-20   |
| bicep  | [Microsoft.App/containerApps template reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/containerapps?pivots=deployment-language-bicep)                                       | not recorded |
| all    | [Microsoft.App all API versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/allversions)                                                                                          | not recorded |
| spec   | [ContainerApps OpenAPI spec, stable/2026-07-01](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/app/resource-manager/Microsoft.App/ContainerApps/stable/2026-07-01/openapi.json) | main branch  |
| mi     | [Managed identities in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity)                                                                                        | 2026-02-13   |
| keda   | [KEDA Azure Service Bus scaler](https://keda.sh/docs/latest/scalers/azure-service-bus/)                                                                                                                | KEDA 2.21    |
| kedaso | [KEDA ScaledObject spec](https://keda.sh/docs/latest/reference/scaledobject-spec/)                                                                                                                     | KEDA 2.21    |
| sas    | [Service Bus access control with SAS](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas)                                                                                   | 2026-10-29   |

## API version for the Container Apps

**Pin `Microsoft.App/containerApps@2026-07-01`.** It is the newest version of all (all, bicep), it isn't a preview, and
it has both properties. Nothing here needs a preview version.

| API version        | Kind    | `pollingInterval` and `cooldownPeriod` on `Scale` | How checked                       |
|--------------------|---------|---------------------------------------------------|-----------------------------------|
| 2026-07-01         | GA      | Yes                                               | reference, spec, local compile    |
| 2026-03-02-preview | Preview | Not checked                                       |                                   |
| 2026-01-01         | GA      | Not checked                                       |                                   |
| 2025-10-02-preview | Preview | Not checked                                       |                                   |
| 2025-07-01         | GA      | Yes                                               | local compile                     |
| 2025-02-02-preview | Preview | Yes                                               | reference                         |
| 2025-01-01         | GA      | Yes: the first GA version that has them           | reference, local compile          |
| 2024-10-02-preview | Preview | Yes                                               | reference                         |
| 2024-03-01         | GA      | No                                                | reference, local compile (BCP037) |

The local compile used Bicep CLI 0.48.1, the latest release, with `az` 2.91.0. A scratch template with the properties
below compiled with no warnings on 2026-07-01, 2025-07-01 and 2025-01-01. On 2024-03-01 it gave warning BCP037 on both
properties, so the compile does tell the versions apart. It makes no Azure call, and it doesn't prove the service
accepts the values.

The docs' own Bicep examples in scale still use `2025-02-02-preview`, which the page doesn't state as a requirement.

## Scale settings

Under `properties.template.scale`:

```bicep
scale: {
  minReplicas: 0
  maxReplicas: 1
  pollingInterval: 10   // seconds, int
  cooldownPeriod: 60    // seconds, int
  rules: [ ... ]
}
```

| Fact                                 | Value                                                                   | Source                     |
|--------------------------------------|-------------------------------------------------------------------------|----------------------------|
| Polling interval default             | 30 s                                                                    | scale, bicep, spec, kedaso |
| Cooldown default                     | 300 s                                                                   | scale, bicep, spec, kedaso |
| Allowed range of either              | **Not documented.** Plain `int32`, no minimum or maximum in the spec    | spec, bicep                |
| Consumption profile restriction      | None stated. The pages don't mention workload profiles here             | scale                      |
| Scale-up stabilization window        | 0 s                                                                     | scale                      |
| Scale-down stabilization window      | 300 s                                                                   | scale                      |
| Min replicas: default, allowed range | 0, 0 to 1,000                                                           | scale                      |
| Max replicas: default, allowed range | 10, 1 to 1,000                                                          | scale                      |
| KEDA version Container Apps runs     | **Not documented** on these pages. The KEDA docs read are upstream 2.21 | scale, keda                |

Open, to settle against a live app: whether 5 s and 10 s polling are accepted and honoured, because no page gives a
lower limit; and the actual polling behaviour, since all that's documented is the setting.

### Where the docs disagree

- **What cooldown applies to.** scale says both "only takes effect when scaling in from the final replica to 0" and
  "how long after the last event KEDA waits before the application scales down to its minimum replica count". kedaso
  agrees with the first. With min replicas 0 the two mean the same, so this doesn't affect the experiment.
- **`messageCount`.** keda defaults it to 5. scale's example says that with `messageCount` "5", "If the queue length is
  greater than 0, scale the app to 1", which fits keda's `activationMessageCount` default of 0. So one message should
  wake the app at any `messageCount`. This experiment sets 1 anyway.
- **Scale-down timing.** scale's example lists both a 300 s stabilization window and a 300 s cooldown. It doesn't say
  whether they add up. It matters for how long the runner waits for zero, and a live app will show it.

## Service Bus scale rule

A `custom` rule with type `azure-servicebus` (scale, bicep):

| Fact                             | Value                                                                             | Source           |
|----------------------------------|-----------------------------------------------------------------------------------|------------------|
| Metadata used                    | `queueName`, `namespace`, `messageCount`                                          | scale, keda      |
| `messageCount` default           | 5                                                                                 | keda             |
| `activationMessageCount` default | 0                                                                                 | keda             |
| Connection string auth           | `auth: [{ secretRef, triggerParameter: 'connection' }]`                           | scale            |
| Rights for the connection string | **Manage**: "Manage access is required for KEDA to be able to get metrics"        | keda, sas        |
| `namespace` metadata             | Required with identity, optional with a connection string                         | keda             |
| Managed identity                 | Supported: `identity` on the custom rule, a user-assigned resource ID or `system` | scale, bicep, mi |
| Azure portal                     | Doesn't support identity on scale rules                                           | scale            |

sas agrees that reading a queue's description needs Manage, but has no row for runtime properties or message counts.

A scale rule using `identity` with a user-assigned ID instead of `auth` also compiled with no warnings on 2026-07-01.

Open: **which RBAC role the scale rule's identity needs.** None of scale, mi or keda names one, and scale's only
identity example is Azure Queue, not Service Bus. It has to be found elsewhere or tested before an identity deployment.

## What this means for the infrastructure

- Pin `2026-07-01`. The scaler gets its own Manage SAS rule, which is more than the worker needs and is kept separate
  from the worker's Listen and Send rule. Whether Manage can be scoped to one queue is still to check.
- Leave `pollingInterval` and `cooldownPeriod` unset by default, and pass them only for the configs that change them.
- Read the settings back from the live app before trusting a config's samples, since the range is undocumented.
