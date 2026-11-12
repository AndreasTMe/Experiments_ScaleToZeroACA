# Costs

What one jit worker (0.25 vCPU, 0.5 GiB, `infra/apps.bicep`) costs in UK South at each scale setting, and what that
buys in latency. Three kinds of figure are kept apart:

- **Calculated**: list rates times a time, in USD before the free grant unless a row says otherwise.
- **Measured usage**: replica time read from the runs and from Azure Monitor.
- **Billed**: what Cost Management shows.

A month is 730 hours (2,628,000 s).

## Sources

| Ref       | What                                                                                                                                                           | Read       |
|-----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|------------|
| billing   | [Billing in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/billing), page date 2025-12-09                                        | 2026-11-02 |
| prices    | [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview), UK South, Consumption; rates in `docs/platform-facts.md` | 2026-11-12 |
| sbpricing | [Service Bus pricing](https://azure.microsoft.com/en-us/pricing/details/service-bus/): "An operation is any API call to the Service Bus service."              | 2026-11-12 |
| runs      | `results/levers-*/samples.csv`: each sample's `scaledOut` and `pullStarted`, and the next sample's `zeroObserved`                                              | 2026-11-12 |
| metrics   | `results/levers-min-1/metrics.json`: `Replicas`, `UsageNanoCores` and `RxBytes` for the 20 hours at min replicas 1                                             | 2026-11-11 |
| usage     | `az consumption usage list` for 2026-11-08 to 2026-11-11, with `-m` for the subscription's meter details; output not committed                                 | 2026-11-11 |

The Container Apps rates were the same on 2026-11-12 as on 2026-11-02. A running replica costs $0.0000105 a second at
the active rate (0.25 × $0.000034 + 0.5 × $0.000004) and $0.000003 at the idle rate (0.25 × $0.000004 + 0.5 ×
$0.000004).

## Measured usage

**Scale to zero: replica time per wake-up.** One trigger, then nothing until the replica is gone. Medians over 29
gaps per run (runs). The start is either the scale-out event or the image pull; billing doesn't say which moment a
replica's billing starts at, so both are given. The end is when the runner saw zero replicas, which it checks every
10 s, so it can be up to 10 s late.

| Run                        | Polling (s) | Cooldown (s) | Scale-out to zero (s) | Pull to zero (s) |
|----------------------------|-------------|--------------|-----------------------|------------------|
| `levers-polling-30`        | 30          | 300          | 336.8                 | 318.3            |
| `levers-polling-30-random` | 30          | 300          | 336.7                 | 318.0            |
| `levers-polling-10`        | 10          | 300          | 357.5                 | 338.4            |
| `levers-polling-5`         | 5           | 300          | 357.9                 | 339.2            |
| `levers-cooldown-60`       | 30          | 60           | 93.3                  | 74.3             |

The cooldown sets most of the replica's life. At 10 s and 5 s polling the replica lived about 21 s longer than at
30 s; nothing in the docs read explains why.

**Min replicas 1, 20 hours** (metrics, 2026-11-09 22:10 to 2026-11-10 18:10 UTC): one replica the whole time, so
72,000 replica-seconds, which is 18,000 vCPU-s and 36,000 GiB-s, 10% of the monthly free grant. CPU averaged about
0.0037 vCPU, under the idle limit of 0.01, with 15 s peaks of 0.023 (during the warm samples) and 0.012.
Received bytes averaged about 630 B/s, under the idle limit of 1,000 B/s, but the busiest 15 s points reach about
1,470 B/s. billing doesn't say over what period the limits are checked, so the replica was billed at the idle rate
for most of the time at best, and some of it may have been active.

## Billed

The usage listing for 2026-11-08 to 2026-11-11 has no Container Apps rows at all, and its quantity and cost columns
are empty for every row (registry, Service Bus, Log Analytics). The scale-to-zero runs and the 20 hours at min
replicas 1 together stayed inside the free grant, so the bill for Container Apps over those days is $0. The
`Environment Management Hour` meter in prices, effective 2026-11-01, wasn't charged either.

So no Container Apps figure here comes from the bill. The calculated figures below are what the same usage costs once
the grant is used up, for example by other apps on the subscription.

## Calculated

**Min replicas 1, per month.** 657,000 vCPU-s and 1,314,000 GiB-s.

| Rate the replica is billed at | Before the grant | After the grant, if nothing else uses it |
|-------------------------------|------------------|------------------------------------------|
| Idle, the whole month         | $7.88            | $5.72                                    |
| Active, the whole month       | $27.59           | $20.03                                   |

The measured 20 hours come to $0.22 at the idle rate and $0.76 at the active rate, and were billed $0.

**Scale to zero, per wake-up.** A wake-up is billed active for as long as the replica runs (billing: with min replicas
0 nothing is billed as idle). Using the scale-out start, the longer of the two:

| Config                 | Cost per wake-up | 1,000 wake-ups a month | Wake-ups the grant covers | Most isolated wake-ups a month | Wake-ups a month where min 1 at idle is cheaper |
|------------------------|------------------|------------------------|---------------------------|--------------------------------|-------------------------------------------------|
| Defaults, 30 s / 300 s | $0.00354         | $3.54                  | 2,138                     | 7,803                          | over 2,229                                      |
| Polling 10 s           | $0.00375         | $3.75                  | 2,014                     | 7,351                          | over 2,100                                      |
| Polling 5 s            | $0.00376         | $3.76                  | 2,012                     | 7,343                          | over 2,098                                      |
| Cooldown 60 s          | $0.00098         | $0.98                  | 7,717                     | 28,167                         | over 8,048                                      |

From the pull start, each wake-up costs $0.0002 less. The worker's 2:1 memory to vCPU matches the grant's, so its
vCPU and memory grants run out at the same wake-up count. "Isolated" means each trigger arrives after the previous
replica has gone; triggers closer together share a replica, and at the most isolated wake-ups a month the replica is
never at zero, so scale to zero costs the same as min 1 at the active rate.

**Does a shorter polling interval change the bill?** Hardly:

- **Container Apps.** Nothing is billed while the app is at zero, however often KEDA checks the queue (billing). The
  only change measured is the longer replica life above, about 6% more per wake-up ($0.0002).
- **Service Bus.** Basic is $0.05 per million operations (prices), and sbpricing counts any API call. If each KEDA
  check is one call, a month of checks on one queue is 87,600 at 30 s ($0.004), 262,800 at 10 s ($0.013) and 525,600
  at 5 s ($0.026). Whether the scaler's call is metered isn't stated, so these are upper bounds.

**Fixed costs, the same for every config:** the registry, about $5 a month, and the Service Bus namespace's
operations from the messages themselves. Neither depends on how the worker scales.

## Latency against cost

jit, one worker, UK South. Latency is the median end-to-end time from trigger enqueued to done enqueued, from each
run's `summary.md`. Cost is calculated at list prices before the free grant.

| Config                       | Run                        | Median latency | p90 latency | Monthly cost                            |
|------------------------------|----------------------------|----------------|-------------|-----------------------------------------|
| Scale to zero, defaults      | `levers-polling-30-random` | 32.7 s         | 43.9 s      | $0.0035 per wake-up; $3.54 per 1,000    |
| Scale to zero, defaults      | `levers-polling-30`        | 43.9 s         | 48.1 s      | as above                                |
| Scale to zero, polling 10 s  | `levers-polling-10`        | 28.0 s         | 31.3 s      | $0.0038 per wake-up; $3.75 per 1,000    |
| Scale to zero, polling 5 s   | `levers-polling-5`         | 23.0 s         | 24.1 s      | $0.0038 per wake-up; $3.76 per 1,000    |
| Scale to zero, cooldown 60 s | `levers-cooldown-60`       | 47.9 s         | 48.8 s      | $0.0010 per wake-up; $0.98 per 1,000    |
| Always on, min replicas 1    | `levers-min-1`             | 0.010 s        | 0.013 s     | $7.88 (all idle) to $27.59 (all active) |

- `levers-polling-30-random` sends each trigger a random time after scale-in, as real traffic would arrive.
  The other scale-to-zero runs send it as soon as zero is seen, which lands just after a KEDA check and waits most of
  an interval. That bias is up to the polling interval, so it's largest at 30 s and smallest at 5 s, and their
  latencies are upper ends.
- Polling and cooldown weren't combined in one run. Polling 5 s with cooldown 60 s would be expected to keep polling
  5 s's latency at about the cooldown run's cost, but that isn't measured.
- `levers-min-1` has no summary file; its median and p90 are from its `samples.csv`, interpolated the same way.
