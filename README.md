# Scale to zero, and the wait to come back

How long a Service Bus message waits when a .NET worker on Azure Container Apps is scaled to zero, where that time
goes, and which part of it can be changed.

A trigger message goes onto a queue whose worker has no replicas. KEDA sees it, Container Apps schedules a replica,
pulls the image and starts the container, the worker starts .NET, connects to Service Bus, receives the message and
sends a done message. The runner times each step from the platform's system logs and the worker's own timestamps.

## Findings

All times are medians of 30 samples, in seconds, from trigger enqueued to done enqueued.

- **Almost all of the wait is the platform.** KEDA polling and scheduling take 42.6 to 44.3 s of a cold start, for every
  variant; everything after them takes 0.4 to 2.4 s ([baseline-cold](results/baseline-cold/summary.md)).
- **The image and runtime decide the last 2 s.** From image pull to done: jit 2.4 s, jit-chiseled 2.1 s, r2r 1.6 s,
  aot 0.4 s. Native AOT starts in 98 ms and connects in 134 ms, where jit takes 641 ms and 1,496 ms.
- **Image size hardly matters once the node has the image.** Cached pulls take about 0.1 s for all four. A full pull
  of a fresh copy took 0.6 s for aot (9.7 MiB), 1.6 s for jit-chiseled (43.4 MiB), 2.6 s for jit (82.5 MiB) and 3.0 s
  for r2r (85.1 MiB) ([forced-pull](results/forced-pull/summary.md), [images](docs/images.md)).
- **A managed identity adds about 3 s to connecting.** The first token took 2.9 s; connecting to Service Bus took 4.2 s
  against 1.3 s with a connection string ([identity-cold](results/identity-cold/summary.md)).
- **The polling interval is the lever.** jit end to end at 30 s polling: 43.9 s; at 10 s: 28.0 s; at 5 s: 23.0 s.
  With the trigger sent at a random time at 30 s polling it's 32.7 s.
- **Warm, a message takes about 10 ms** ([baseline-warm](results/baseline-warm/summary.md)), and with one replica
  always on it's 10 ms too (`results/levers-min-1/samples.csv`).
- **Scale to zero is cheap at low traffic.** At 1,000 wake-ups a month and list prices, the defaults cost \$3.54, a
  60 s cooldown \$0.98, and 5 s polling \$3.76. One replica always on costs \$7.88 billed idle to \$27.59 billed active,
  so above about 2,200 wake-ups a month on the defaults it's the cheaper choice ([costs](docs/costs.md)).

| Run                                                                     | Polling (s) | Cooldown (s) | End to end | Polling phase |
|-------------------------------------------------------------------------|-------------|--------------|------------|---------------|
| [levers-polling-5](results/levers-polling-5/summary.md)                 | 5           | 300          | 23.0 s     | 2.0 s         |
| [levers-polling-10](results/levers-polling-10/summary.md)               | 10          | 300          | 28.0 s     | 6.9 s         |
| [levers-polling-30](results/levers-polling-30/summary.md)               | 30          | 300          | 43.9 s     | 23.1 s        |
| [levers-polling-30-random](results/levers-polling-30-random/summary.md) | 30          | 300          | 32.7 s     | 11.1 s        |
| [levers-cooldown-60](results/levers-cooldown-60/summary.md)             | 30          | 60           | 47.9 s     | 26.6 s        |

### Read this before quoting a number

Every run except `levers-polling-30-random` sends its trigger as soon as the runner sees the app at zero. That lands
just after a KEDA check, so the polling phase is close to a whole interval and the end-to-end times are upper ends of
what a real message would wait. The bias is up to one polling interval: largest at 30 s, smallest at 5 s.
`levers-polling-30-random` sends each trigger a random time up to one interval later, as real traffic would arrive.

Scheduling, about 19 s in every run, didn't change with any setting tried here.

## Setup

| What                | Value                                                                                          |
|---------------------|------------------------------------------------------------------------------------------------|
| Region              | UK South                                                                                       |
| Container Apps      | Consumption plan; each app 0.25 vCPU, 0.5 GiB, max replicas 1                                  |
| Service Bus         | Basic namespace, one queue per variant plus `done`                                             |
| Container Registry  | Basic                                                                                          |
| Log Analytics       | PerGB2018, 30-day retention                                                                    |
| .NET                | SDK 10.0.403 (`global.json`, no roll-forward), runtime 10.0.14, `net10.0`                      |
| Packages            | Azure.Messaging.ServiceBus 7.21.0, Azure.Identity 1.21.0, Microsoft.Extensions.Hosting 10.0.14 |
| Tools               | Azure CLI 2.91.0, Bicep CLI 0.49.2                                                             |
| Defaults (platform) | KEDA polling 30 s, cooldown 300 s                                                              |
| Runs                | 2026-11-07 to 2026-11-12                                                                       |

The five worker variants are built from one Dockerfile, with base images pinned by digest:

| Variant        | Code                                     | Base image                            | Compressed   |
|----------------|------------------------------------------|---------------------------------------|--------------|
| `jit`          | IL, framework-dependent                  | `runtime:10.0.14-noble`               | 82.5 MiB     |
| `r2r`          | IL plus ReadyToRun code                  | `runtime:10.0.14-noble`               | 85.1 MiB     |
| `aot`          | Native AOT binary                        | `runtime-deps:10.0.14-noble-chiseled` | 9.7 MiB      |
| `jit-chiseled` | IL, framework-dependent                  | `runtime:10.0.14-noble-chiseled`      | 43.4 MiB     |
| `jit-identity` | `jit` signing in with a managed identity | `runtime:10.0.14-noble`               | not measured |

Sizes, digests and how they were measured are in [docs/images.md](docs/images.md); the `jit-identity` image is 25,758
bytes larger than `jit` uncompressed.

## Repository

| Path                  | What                                                                                                                                                |
|-----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `src/Worker`          | The worker and its Dockerfile                                                                                                                       |
| `scripts/InfraRunner` | Deploys the Bicep templates and pushes images                                                                                                       |
| `scripts/Runner`      | Sends triggers, collects samples and writes the summaries                                                                                           |
| `infra`               | Bicep: the base resources, the apps, and the scale configs in `infra/levers`                                                                        |
| `local`               | Service Bus emulator for running the worker locally                                                                                                 |
| `results/<run>`       | Each run's `config.json`, `samples.csv`, and `summary.md` and `summary.json`                                                                        |
| `charts`              | Astro chart components and the script that exports their data ([docs](docs/charts.md))                                                              |
| `docs`                | [Images](docs/images.md), [Native AOT](docs/aot.md), [costs](docs/costs.md), [defaults](docs/defaults.md), [platform facts](docs/platform-facts.md) |

## Running it

You need the .NET SDK from `global.json`, Docker, the Azure CLI and a subscription. Deploying creates billable
resources in your subscription; check the templates first.

```powershell
az login
az account set --subscription $sub

dotnet run --project scripts/InfraRunner -- deploy base --subscription-id $sub --budget-email $email
dotnet run --project scripts/InfraRunner -- push-images --subscription-id $sub --image-tag $tag
dotnet run --project scripts/InfraRunner -- deploy apps --subscription-id $sub --image-tag $tag
```

A scale config from `infra/levers` is deployed with `--config <name>`, for example `--config jit-polling-10`;
`--preview` shows the change without making it.

The runner reads the Service Bus connection string from `SERVICEBUS_CONNECTION_STRING`:

```powershell
$ns = az servicebus namespace list --resource-group rg-stz-uksouth --query "[0].name" -o tsv
$env:SERVICEBUS_CONNECTION_STRING = az servicebus namespace authorization-rule keys list --resource-group rg-stz-uksouth --namespace-name $ns --name worker --query primaryConnectionString -o tsv

dotnet run --project scripts/Runner -- sample --variant jit,r2r,aot,jit-chiseled --samples 30 --run my-cold-run
dotnet run --project scripts/Runner -- sample --variant jit,r2r,aot,jit-chiseled --samples 30 --warm --run my-warm-run
dotnet run --project scripts/Runner -- analyze my-cold-run --warm my-warm-run
```

`--random-delay` sends each trigger a random time after scale-in instead of straight away. Each run writes to
`results/<run>`.

Delete the resource group when you're done:

```powershell
az group delete --name rg-stz-uksouth
```
