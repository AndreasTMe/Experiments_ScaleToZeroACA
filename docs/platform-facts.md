# Platform facts

What the experiment relies on from Azure, Container Apps and KEDA, each with where it was read. "Read" is when the
page was read. "Page date" is the date in the page's own metadata.

## Sources

| Ref      | Page                                                                                                                                                                                                   | Page date        | Read       |
|----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------|------------|
| scale    | [Set scaling rules in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)                                                                                          | 2026-05-20       | 2026-11-01 |
| bicep    | [Microsoft.App/containerApps template reference](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/containerapps?pivots=deployment-language-bicep)                                       | not recorded     | 2026-11-01 |
| all      | [Microsoft.App all API versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/allversions)                                                                                          | not recorded     | 2026-11-01 |
| spec     | [ContainerApps OpenAPI spec, stable/2026-07-01](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/app/resource-manager/Microsoft.App/ContainerApps/stable/2026-07-01/openapi.json) | main branch      | 2026-11-01 |
| mi       | [Managed identities in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity)                                                                                        | 2026-02-13       | 2026-11-01 |
| keda     | [KEDA Azure Service Bus scaler](https://keda.sh/docs/latest/scalers/azure-service-bus/)                                                                                                                | KEDA 2.21        | 2026-11-01 |
| kedaso   | [KEDA ScaledObject spec](https://keda.sh/docs/latest/reference/scaledobject-spec/)                                                                                                                     | KEDA 2.21        | 2026-11-01 |
| sas      | [Service Bus access control with SAS](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-sas)                                                                                   | 2026-10-29       | 2026-11-01 |
| billing  | [Billing in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/billing)                                                                                                      | 2025-12-09       | 2026-11-02 |
| pricing  | [Azure Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)                                                                                                      | not shown        | 2026-11-02 |
| prices   | [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview), filtered to `armRegionName eq 'uksouth'` and `priceType eq 'Consumption'`                        | per meter        | 2026-11-02 |
| stream   | [View log streams in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/log-streaming)                                                                                       | 2025-06-25       | 2026-11-02 |
| logging  | [Application logging in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/logging)                                                                                          | 2025-11-07       | 2026-11-02 |
| logopt   | [Log storage and monitoring options](https://learn.microsoft.com/en-us/azure/container-apps/log-options)                                                                                               | 2026-08-18       | 2026-11-02 |
| logmon   | [Monitor logs with Log Analytics](https://learn.microsoft.com/en-us/azure/container-apps/log-monitoring)                                                                                               | 2026-06-01       | 2026-11-02 |
| clilogs  | [az containerapp logs](https://learn.microsoft.com/en-us/cli/azure/containerapp/logs?view=azure-cli-latest)                                                                                            | 2025-10-14       | 2026-11-02 |
| roles    | [Azure built-in roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles)                                                                                               | 2026-09-10       | 2026-11-02 |
| rolesint | [Azure built-in roles for Integration](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/integration)                                                                   | 2026-10-29       | 2026-11-02 |
| acrroles | [ACR built-in roles overview](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-built-in-roles-overview)                                                              | 2026-03-25       | 2026-11-02 |
| acrabac  | [ACR ABAC repository permissions](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-rbac-abac-repository-permissions)                                                      | 2025-12-11       | 2026-11-02 |
| acrcli   | [Create a registry with the Azure CLI](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-get-started-azure-cli)                                                            | 2023-10-31       | 2026-11-02 |
| clirel   | [Azure CLI release notes](https://learn.microsoft.com/en-us/cli/azure/release-notes-azure-cli)                                                                                                         | 2026-10-06 entry | 2026-11-02 |

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

## Cost in UK South

List prices in USD from prices, Consumption plan (`skuName` Standard). The pricing page didn't render its numbers when
fetched, so the rates are from the API only.

| Meter                   | Rate                                                                                  | Effective from | Source           |
|-------------------------|---------------------------------------------------------------------------------------|----------------|------------------|
| vCPU, active            | $0.000034 per vCPU-second                                                             | 2022-06-01     | prices           |
| vCPU, idle              | $0.000004 per vCPU-second                                                             | 2022-06-01     | prices           |
| Memory, active          | $0.000004 per GiB-second                                                              | 2022-06-01     | prices           |
| Memory, idle            | $0.000004 per GiB-second                                                              | 2022-06-01     | prices           |
| Requests                | $0.40 per million                                                                     | 2022-06-01     | prices           |
| Free grant              | 180,000 vCPU-s, 360,000 GiB-s, 2 million requests per subscription per calendar month |                | billing, pricing |
| Revision scaled to zero | No usage charge                                                                       |                | billing, pricing |

**Idle rate.** billing applies it only when all of these hold: the revision has min replicas above 0 and is scaled to
that minimum; all containers have started and are running; no HTTP requests are being processed; the replica uses
less than 0.01 vCPU; and it receives less than 1,000 bytes per second. With min replicas 0 nothing is billed as idle,
so a running replica is billed as active until it's removed. In UK South idle memory costs the same as active memory;
only vCPU is cheaper.

At 0.25 vCPU and 0.5 GiB, a replica that runs for 6 minutes per wake-up uses 90 vCPU-s and 180 GiB-s, so the vCPU
grant covers about 2,000 wake-ups a month, minus anything else on the subscription.

**Environment charges: new and not explained.** prices lists `Environment Management Hour`, `Environment Planned
Maintenance Hour` and `Environment Private Endpoint` at $0.10 per hour each, all effective 2026-11-01. Neither billing
nor pricing uses these names. billing says private endpoints and planned maintenance carry a "Dedicated Plan
Management" charge on either plan, and also says "You aren't billed any plan management charges unless you use a
Dedicated workload profile in your environment". If a Consumption-only environment with neither feature were charged
$0.10 per hour, that's about $73 a month, and a $10 budget is used up in about 4 days. Open: check Cost Management a day
after the environment is created.

Other resources, from prices:

| Resource                      | Rate                                                                          | Effective from |
|-------------------------------|-------------------------------------------------------------------------------|----------------|
| Container Registry Basic      | $0.1666 per day (about $5 a month), plus $0.10 per GB-month for "Data Stored" | 2018-01-01     |
| Service Bus Basic             | $0.05 per million operations                                                  | 2014-11-01     |
| Log Analytics, Analytics Logs | Ingestion: first 5 GB at $0, then $2.88 per GB. Retention: $0.13 per GB-month | 2018-02-01     |

The registry alone is about half of a $10 monthly budget. The API doesn't say what the free 5 GB of ingestion is counted
per, or how much registry storage is included before "Data Stored" applies.

## System logs

Places to read system events (image pull, container start and so on) from:

| Source                                                 | Needs a workspace                                                              | How much                                   | Scope                                                                     | Source          |
|--------------------------------------------------------|--------------------------------------------------------------------------------|--------------------------------------------|---------------------------------------------------------------------------|-----------------|
| `az containerapp logs show --type system`              | None stated                                                                    | `--tail` 0 to 300 past entries, default 20 | One app. `--revision`, `--replica`, `--container` are for non-system logs | clilogs, stream |
| `az containerapp env logs show`                        | None stated                                                                    | `--tail` 0 to 300, default 20              | The whole environment                                                     | stream          |
| Log Analytics table `ContainerAppSystemLogs_CL`        | Yes: destination `log-analytics`                                               | The workspace's retention                  | KQL                                                                       | logmon, logopt  |
| Diagnostic settings, category `ContainerAppSystemLogs` | Destination `azure-monitor`, sent to a workspace, storage account or event hub | The destination's                          | Up to five settings per environment                                       | logopt          |

What the docs leave open:

- **Without a workspace.** logopt says that with "Don't save logs" you "can still view real-time container logs via
  the Logs stream feature". No page says the same of system logs, and no page makes the stream depend on the
  destination either.
- **Fields and time precision of the stream: not documented.** No page shows its output. `--format` is `json`
  (default) or `text`.
- **How far back the stream goes, and whether a removed replica's events are still there: not documented.** The help
  for `--replica` says "A replica may not exist if there is not traffic to your app."
- **The workspace table.** logmon lists `ContainerAppName_s`, `EnvironmentName_s`, `Log_s` and `RevisionName_s` as the
  most used columns, plus `TimeGenerated`; its examples also use `LogLevel_s`. No replica name column is listed, but the
  list isn't complete. The precision of `TimeGenerated` isn't stated. logopt says records take "several minutes" to be
  queryable.
- **Event names** such as pulling, pulled or started: not documented. logging and logmon only list revision-level
  messages, such as "Successfully provisioned revision" and "ErrorCode: ErrImagePull".

So no page shows that any source gives per-replica events with sub-second times. The stream is the only one without a
workspace and costs nothing, so try it first: right after the first live sample, save the output of
`az containerapp logs show --type system --format json --tail 300` and see whether it names the replica and how
precise its times are. The workspace, with `dailyQuotaGb` capped, is the fallback.

Tried on 2026-11-07: the stream names the replica and the reason, but its times are whole seconds and one app's
replica events were already gone when read. So the workspace is switched on in `main.bicepparam`, capped at 1 GB a
day. `dailyQuotaGb` is an int, so that's the smallest cap. The daily cap page (2026-01-09) says the cap "can't stop
data collection at precisely the specified cap level", gives no minimum, and says collection resumes at a reset hour
that can't be configured.

Read from the workspace on 2026-11-07, one cold sample per app:

- **Columns.** `getschema` lists `ReplicaName_s`, `Reason_s`, `Count_d`, `Type_s`, `EventSource_s`, `TimeStamp_s`,
  `time_s` and `_timestamp_d` besides the documented ones.
- **Event time is `time_s`.** It's RFC 3339 with nanoseconds. `_timestamp_d` is the same instant as Unix seconds, to
  about 10 µs. `TimeStamp_s` is whole seconds. `TimeGenerated` is the ingestion batch: several apps share one value,
  and it puts the pull after the container start, so it isn't used.
- **Reasons on a replica**, in order: `AssigningReplica`, `ContainerCreated` and `ContainerStarted` twice,
  `PullingImage`, `ImagePulled` (the pull time and image size in the message), `ContainerCreated`,
  `ContainerStarted`, and `StoppingContainer` at scale-in. The created and started events don't name the container,
  so the pair after `ImagePulled` is taken as the worker's. The two pairs before it are assumed to be platform
  containers; no page says so.
- **The scale-out event** ("Scaled apps/v1.Deployment ... from 0 to 1, triggered by s0-azure-servicebus-...") has an
  empty `ReplicaName_s` and carries only the revision.
- On jit, about 18 s passed between `AssigningReplica` and `PullingImage`; the pull itself took 109 ms.
- **Console logs** are in `ContainerAppConsoleLogs_CL`. `ContainerGroupName_s` equals the system log's
  `ReplicaName_s`, so the two tables join on it, and `ContainerName_s` is `worker`. Only the worker wrote console
  lines, so the other containers don't show there. `time_s` has nanoseconds here too. The simple console logger
  writes each entry as two rows, the header with its timestamp and then the message.
- The "Handled" line carries the trigger's message id, which the runner sets to the trigger id, so a sample's done
  sent time can be read from it. The worker's process start (17:15:31.127) came about 80 ms before the system log's
  `ContainerStarted` (17:15:31.207), so that event is logged after the process is already running.

## API versions for the other resources

The newest non-preview version of each type in the template reference, read 2026-11-02. Every template reference page
carries the same `ms.date` (2024-12-09), so the page date here is the page's updated date.

| Resource type                                               | Pin        | Newest preview     | Reference                                                                                                                            | Page date  |
|-------------------------------------------------------------|------------|--------------------|--------------------------------------------------------------------------------------------------------------------------------------|------------|
| `Microsoft.Resources/resourceGroups`                        | 2025-04-01 | none               | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.resources/allversions)                                    | 2026-09-21 |
| `Microsoft.Consumption/budgets`                             | 2026-06-01 | 2019-05-01-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.consumption/allversions)                                  | 2026-09-21 |
| `Microsoft.App/managedEnvironments`                         | 2026-07-01 | 2026-03-02-preview | [2026-07-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/2026-07-01/managedenvironments)                         | 2026-09-21 |
| `Microsoft.App/containerApps`                               | 2026-07-01 | 2026-03-02-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/allversions)                                          | 2026-10-20 |
| `Microsoft.ServiceBus/namespaces`                           | 2026-01-01 | 2026-07-01-preview | [2026-01-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces)                           | 2026-09-21 |
| `Microsoft.ServiceBus/namespaces/queues`                    | 2026-01-01 | 2026-07-01-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/allversions)                                   | 2026-09-21 |
| `Microsoft.ServiceBus/namespaces/authorizationRules`        | 2026-01-01 | 2026-07-01-preview | [2026-01-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces/authorizationrules)        | 2026-10-20 |
| `Microsoft.ServiceBus/namespaces/queues/authorizationRules` | 2026-01-01 | 2026-07-01-preview | [2026-01-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.servicebus/2026-01-01/namespaces/queues/authorizationrules) | 2026-10-20 |
| `Microsoft.ContainerRegistry/registries`                    | 2025-11-01 | 2026-09-01-preview | [2025-11-01](https://learn.microsoft.com/en-us/azure/templates/microsoft.containerregistry/2025-11-01/registries)                    | 2026-10-20 |
| `Microsoft.ManagedIdentity/userAssignedIdentities`          | 2024-11-30 | 2025-05-31-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.managedidentity/allversions)                              | 2026-04-06 |
| `Microsoft.Authorization/roleAssignments`                   | 2022-04-01 | 2020-10-01-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.authorization/allversions)                                | 2026-10-20 |
| `Microsoft.OperationalInsights/workspaces`                  | 2026-03-01 | 2021-12-01-preview | [all versions](https://learn.microsoft.com/en-us/azure/templates/microsoft.operationalinsights/allversions)                          | 2026-09-21 |

A scratch template with all twelve types at these versions compiled and linted with no warnings on Bicep CLI 0.48.1.
With one version changed to one that doesn't exist (`namespaces@2026-02-01`) the compile gives BCP081, so it does check
the versions. It doesn't check the values of `environmentMode` or `roleAssignmentMode`: wrong strings compiled with no
warning.

Properties the infrastructure will set, from the version pages above:

| Resource                 | Property                                                       | Values                                                                                                             | Note                                                                                                                      |
|--------------------------|----------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| managedEnvironments      | `appLogsConfiguration.destination`                             | `log-analytics`, `azure-monitor`, `none` (the API rejects `none`, see below)                                       | `logAnalyticsConfiguration` only with `log-analytics`                                                                     |
| managedEnvironments      | `environmentMode`                                              | `ConsumptionOnly`, `WorkloadProfiles`, `Express`, `Archived`                                                       | **Not explained.** The page doesn't say whether leaving out `workloadProfiles` alone gives a Consumption-only environment |
| Service Bus namespaces   | `sku.name`                                                     | `Basic`, `Standard`, `Premium`                                                                                     |                                                                                                                           |
| Service Bus namespaces   | `disableLocalAuth`                                             | bool                                                                                                               | "disables SAS authentication", so it stays off while the apps use connection strings                                      |
| Both authorization rules | `rights`                                                       | `Listen`, `Manage`, `Send`                                                                                         |                                                                                                                           |
| registries               | `sku.name`, `adminUserEnabled`                                 | `Basic`, `Classic`, `Standard`, `Premium`; bool                                                                    |                                                                                                                           |
| registries               | `roleAssignmentMode`                                           | `AbacRepositoryPermissions`, `LegacyRegistryPermissions`                                                           | No default stated; see below                                                                                              |
| roleAssignments          | `principalType`                                                | `ServicePrincipal`, `User`, `Group` and others                                                                     | Optional. `principalId` and `roleDefinitionId` are required                                                               |
| budgets                  | `timePeriod.startDate`                                         | First of a month, from 2017-06-01, at most 12 months ahead                                                         | `endDate` defaults to 10 years after the start                                                                            |
| budgets                  | `notifications`                                                | Up to five; `operator`, `threshold` (0 to 1000 percent), `thresholdType` (`Actual`, `Forecasted`), `contactEmails` | A subscription budget needs at least one email or action group                                                            |
| workspaces               | `sku.name`, `retentionInDays`, `workspaceCapping.dailyQuotaGb` | `PerGB2018` and others; "per pricing plan"; int                                                                    | No range stated for either int                                                                                            |

**Registry role mode.** acrabac: "`AcrPull`, `AcrPush`, and `AcrDelete` roles aren't honored in an ABAC-enabled
registry", where `Container Registry Repository Reader` replaces `AcrPull` (acrroles). `az acr create` says "The
Default is rbac" (allowed `rbac`, `rbac-abac`), and acrcli's output shows `rbac-abac` as `AbacRepositoryPermissions`.
That `rbac` is `LegacyRegistryPermissions` is inferred, not stated, and acrabac says ABAC "will become the default ...
in the future". Setting `roleAssignmentMode` explicitly keeps `AcrPull` valid whatever the default becomes.

## Built-in roles

| Role                            | Role definition ID                     | Source   |
|---------------------------------|----------------------------------------|----------|
| AcrPull                         | `7f951dda-4ed3-4680-a7ca-43fe172d538d` | roles    |
| Azure Service Bus Data Receiver | `4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0` | rolesint |
| Azure Service Bus Data Sender   | `69a216fc-b8fb-44d8-bc22-1f3c2cd27a39` | rolesint |
| Azure Service Bus Data Owner    | `090c5cfd-751d-490a-894a-3ce6f1109419` | rolesint |

Which of these the scale rule's identity needs is still open (see the Service Bus scale rule above).

## az commands

Checked on 2026-11-02 against each command's reference page and the local `--help` of Azure CLI 2.91.0, which clirel
lists as the newest release. All `containerapp` commands below are in core `az` in 2.91.0; no extension is installed
locally. Reference pages are under `https://learn.microsoft.com/en-us/cli/azure/`, with `?view=azure-cli-latest`.

| Command                                                | Flags to use                                                                                                  | Status                            | Reference page                                                                                                         |
|--------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|-----------------------------------|------------------------------------------------------------------------------------------------------------------------|
| `az account show`                                      | `-o json`; the tools read `id` from the result                                                                | Core, GA                          | [account](https://learn.microsoft.com/en-us/cli/azure/account?view=azure-cli-latest)                                   |
| `az account set`                                       | `--subscription` (required)                                                                                   | Core, GA                          | [account](https://learn.microsoft.com/en-us/cli/azure/account?view=azure-cli-latest)                                   |
| `az version`                                           | `-o json` (JSON is the default)                                                                               | Core, GA                          | [reference index](https://learn.microsoft.com/en-us/cli/azure/reference-index?view=azure-cli-latest)                   |
| `az bicep build`                                       | `--file` (required), `--stdout` or `--outfile`                                                                | Core, GA                          | [bicep](https://learn.microsoft.com/en-us/cli/azure/bicep?view=azure-cli-latest)                                       |
| `az bicep lint`                                        | `--file` (required)                                                                                           | Core, GA                          | [bicep](https://learn.microsoft.com/en-us/cli/azure/bicep?view=azure-cli-latest)                                       |
| `az deployment sub what-if`                            | `--location` (required), `--template-file`, `--parameters`, `--name`                                          | Core, GA                          | [deployment sub](https://learn.microsoft.com/en-us/cli/azure/deployment/sub?view=azure-cli-latest)                     |
| `az deployment sub create`                             | `--location` (required), `--template-file`, `--parameters`, `--name`; `--confirm-with-what-if`                | Core, GA                          | [deployment sub](https://learn.microsoft.com/en-us/cli/azure/deployment/sub?view=azure-cli-latest)                     |
| `az deployment sub show`                               | `--name` (required), `-o json`; read `properties.outputs.<name>.value`                                        | Core, GA                          | [deployment sub](https://learn.microsoft.com/en-us/cli/azure/deployment/sub?view=azure-cli-latest)                     |
| `az deployment group what-if`                          | `--resource-group` (required), `--parameters`, `--name`                                                       | Core, GA                          | [deployment group](https://learn.microsoft.com/en-us/cli/azure/deployment/group?view=azure-cli-latest)                 |
| `az deployment group create`                           | `--resource-group` (required), `--parameters`, `--name`                                                       | Core, GA                          | [deployment group](https://learn.microsoft.com/en-us/cli/azure/deployment/group?view=azure-cli-latest)                 |
| `az acr login`                                         | `--name` (required); `--expose-token` to get a token without Docker                                           | Core, GA                          | [acr](https://learn.microsoft.com/en-us/cli/azure/acr?view=azure-cli-latest)                                           |
| `az acr repository show`                               | `--name` (required, the registry), `--image name:tag`                                                         | Core, GA                          | [acr repository](https://learn.microsoft.com/en-us/cli/azure/acr/repository?view=azure-cli-latest)                     |
| `az acr manifest list-metadata`                        | `--registry`, `--name` (the repository)                                                                       | Core, **preview**                 | [acr manifest](https://learn.microsoft.com/en-us/cli/azure/acr/manifest?view=azure-cli-latest)                         |
| `az containerapp replica list`                         | `--name`, `--resource-group` (both required); `--revision` defaults to the latest                             | Core, GA                          | [containerapp replica](https://learn.microsoft.com/en-us/cli/azure/containerapp/replica?view=azure-cli-latest)         |
| `az containerapp show`                                 | `--name`, `--resource-group`, `-o json`; the runner reads `properties.template`                               | Core, GA                          | [containerapp](https://learn.microsoft.com/en-us/cli/azure/containerapp?view=azure-cli-latest)                         |
| `az containerapp update`                               | `--name`, `--resource-group`, `--image`, `--revision-suffix`                                                  | Core, GA                          | [containerapp](https://learn.microsoft.com/en-us/cli/azure/containerapp?view=azure-cli-latest)                         |
| `az containerapp revision list`                        | `--name`, `--resource-group` (both required), `--all`                                                         | Core, GA                          | [containerapp revision](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision?view=azure-cli-latest)       |
| `az containerapp revision show`                        | `--revision` (required), `--name`, `--resource-group`                                                         | Core, GA                          | [containerapp revision](https://learn.microsoft.com/en-us/cli/azure/containerapp/revision?view=azure-cli-latest)       |
| `az containerapp logs show`                            | `--name`, `--resource-group` (both required), `--type system`, `--format json`, `--tail` (0 to 300)           | Core, GA                          | [containerapp logs](https://learn.microsoft.com/en-us/cli/azure/containerapp/logs?view=azure-cli-latest)               |
| `az containerapp env logs show`                        | `--name`, `--resource-group`, `--tail` (0 to 300)                                                             | Core, GA                          | [containerapp env](https://learn.microsoft.com/en-us/cli/azure/containerapp/env?view=azure-cli-latest)                 |
| `az servicebus namespace authorization-rule keys list` | `--resource-group`, `--namespace-name`, `--name` (all required)                                               | Core, GA                          | [keys](https://learn.microsoft.com/en-us/cli/azure/servicebus/namespace/authorization-rule/keys?view=azure-cli-latest) |
| `az servicebus queue authorization-rule keys list`     | `--resource-group`, `--namespace-name`, `--queue-name`, `--name` (all required)                               | Core, GA                          | [keys](https://learn.microsoft.com/en-us/cli/azure/servicebus/queue/authorization-rule/keys?view=azure-cli-latest)     |
| `az group exists`                                      | `--name` (required)                                                                                           | Core, GA                          | [group](https://learn.microsoft.com/en-us/cli/azure/group?view=azure-cli-latest)                                       |
| `az group delete`                                      | `--name` (required), `--yes`, `--no-wait`                                                                     | Core, GA                          | [group](https://learn.microsoft.com/en-us/cli/azure/group?view=azure-cli-latest)                                       |
| `az consumption budget show`, `delete`                 | `--budget-name` (required)                                                                                    | Core, **preview**                 | [consumption budget](https://learn.microsoft.com/en-us/cli/azure/consumption/budget?view=azure-cli-latest)             |
| `az monitor log-analytics query`                       | `--workspace` (required, "GUID of the Log Analytics Workspace"), `--analytics-query` (required), `--timespan` | **Extension** `log-analytics`, GA | [monitor log-analytics](https://learn.microsoft.com/en-us/cli/azure/monitor/log-analytics?view=azure-cli-latest)       |

Notes:

- The keys pages show no output, so the field name for the connection string (expected `primaryConnectionString`)
  is to be confirmed on the first run.
- `az containerapp replica count` exists only in the `containerapp` extension, as a preview, so the runner counts the
  result of `replica list` instead.
- The runner and InfraRunner ask for `-o json` and read fields from the result rather than passing `--query`,
  because on Windows `az` runs through `cmd.exe`, which would reinterpret characters in JMESPath queries.
- A `.bicepparam` file can be passed to `--parameters` without `--template-file`, with a second `--parameters`
  of inline `key=value` overrides that take precedence over the file (Bicep parameter-files page, 2026-06-17; Azure
  CLI 2.53.0 or later). The deployment group page adds that a `.bicepparam` file can be given only once.
- The `.bicepparam` file is built on its own first, so it has to assign every parameter without a default, even
  one that's also passed inline: otherwise the build stops with BCP258 (seen on a real `deploy base`). The two files
  read `budgetEmail` and `imageTag` with `readEnvironmentVariable` from `STZ_BUDGET_EMAIL` and `STZ_IMAGE_TAG`, which
  InfraRunner sets for the `az` call. With the variable unset, `az bicep build-params` stops with BCP427 (Bicep
  0.49.2; functions page, 2026-06-26).
- Neither the `az group exists` nor the `az deployment sub show` page shows output, so the `true` or `false` of the
  first and the `properties.outputs.<name>.value` path of the second are to be confirmed on the first run.
  InfraRunner stops with an error if either is missing.
- `az monitor log-analytics query` needs the `log-analytics` extension, which isn't installed here. Running it offers to
  install it, so a script should install it explicitly rather than rely on the prompt. It's only needed if the
  workspace is switched on.
- Whether `az resource delete` can remove a budget isn't covered by any page read; `az consumption budget delete` is
  the documented way.

## What this means for the infrastructure

- Pin `2026-07-01`. The scaler gets its own Manage SAS rule, which is more than the worker needs and is kept separate
  from the worker's Listen and Send rule. Whether Manage can be scoped to one queue is still to check.
- Leave `pollingInterval` and `cooldownPeriod` unset by default, and pass them only for the configs that change them.
- Read the settings back from the live app before trusting a config's samples, since the range is undocumented.
- Pin the versions in the table above. Leave out `appLogsConfiguration` to keep logs off (a real `deploy base` failed on
  `destination: 'none'` with "App Logs destination 'none' not supported"), and set the registry's `roleAssignmentMode`
  explicitly so `AcrPull` applies. `environmentMode: 'ConsumptionOnly'` is the likely way to keep the environment
  Consumption-only, but the docs don't explain it, so read the mode back after the first deploy.
- The registry is the main fixed cost, about $5 a month. Watch for the new environment management charge in Cost
  Management, since it alone would exceed a $10 budget in days.
