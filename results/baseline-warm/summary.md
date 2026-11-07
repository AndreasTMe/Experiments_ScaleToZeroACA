# baseline-warm

120 warm samples. Times are in milliseconds; the median and p90 are interpolated linearly between samples.

## End-to-end

| Variant | Polling (s) | Cooldown (s) | Samples | Ok | Median | p90 | Max |
|---|---|---|---|---|---|---|---|
| jit-chiseled | 30 | 300 | 30 | 30 | 6 | 22 | 23 |
| jit | 30 | 300 | 30 | 30 | 10.5 | 25.1 | 44 |
| r2r | 30 | 300 | 30 | 30 | 12 | 25 | 30 |
| aot | 30 | 300 | 30 | 30 | 10 | 17.8 | 31 |

Every sample is ok.

## Phase medians

Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; the sum error is the largest gap between one's phases added up and its end-to-end time.

| Variant | delivery | handling | Sum of medians | Complete samples | Sum error |
|---|---|---|---|---|---|
| jit-chiseled | 17 | -7.9 | 9.1 | 30 | 0 |
| jit | 20.5 | -8.1 | 12.4 | 30 | 0 |
| r2r | 20.4 | -7.8 | 12.6 | 30 | 0 |
| aot | 18 | -7.4 | 10.6 | 30 | 0 |

- delivery: triggerEnqueued to messageReceived
- handling: messageReceived to doneEnqueued
