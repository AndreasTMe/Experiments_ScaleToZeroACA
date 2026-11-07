# baseline-cold

120 cold samples. Times are in milliseconds; the median and p90 are interpolated linearly between samples.
The warm medians are from baseline-warm.

## End-to-end

| Variant | Polling (s) | Cooldown (s) | Samples | Ok | Median | p90 | Max | Warm median | Cold − warm median |
|---|---|---|---|---|---|---|---|---|---|
| aot | 30 | 300 | 30 | 30 | 41162.5 | 46410 | 46898 | 10 | 41152.5 |
| jit-chiseled | 30 | 300 | 30 | 30 | 43779 | 47959.3 | 49967 | 6 | 43773 |
| r2r | 30 | 300 | 30 | 30 | 45288 | 48192.4 | 49346 | 12 | 45276 |
| jit | 30 | 300 | 30 | 30 | 44229 | 46811.6 | 48398 | 10.5 | 44218.5 |

Every sample is ok.

## Phase medians

Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; the sum error is the largest gap between one's phases added up and its end-to-end time.

| Variant | polling | scheduling | imagePull | containerStart | dotnetStartup | serviceBusConnection | handling | Sum of medians | Complete samples | Sum error |
|---|---|---|---|---|---|---|---|---|---|---|
| aot | 24494.5 | 18914.1 | 108.6 | 31.3 | 97.6 | 133.5 | -6 | 43773.6 | 30 | 0 |
| jit-chiseled | 24296.3 | 18919.4 | 99.1 | 33.6 | 623.4 | 1218.9 | 140.5 | 45331.2 | 30 | 0 |
| r2r | 25251.5 | 19080.8 | 97.9 | 43.2 | 447 | 977.8 | 71.8 | 45970 | 30 | 0 |
| jit | 23518.3 | 19061.6 | 103 | 39 | 640.8 | 1495.9 | 107 | 44965.6 | 30 | 0 |

- polling: triggerEnqueued to scaledOut
- scheduling: scaledOut to pullStarted
- imagePull: pullStarted to imagePulled
- containerStart: imagePulled to processStarted
- dotnetStartup: processStarted to hostStarted
- serviceBusConnection: hostStarted to messageReceived
- handling: messageReceived to doneEnqueued

## Image pulls

A first pull is a sample whose image or digest differs from the previous sample's. The first sample of a run is unknown, since the node may have its image from an earlier run. The pull time is the one the ImagePulled event reports.

| Variant | Pull | Samples | Pull median | Pull p90 | End-to-end median |
|---|---|---|---|---|---|
| aot | cached | 29 | 111 | 158.4 | 41308 |
| aot | unknown | 1 | 98 | 98 | 25923 |
| jit-chiseled | cached | 29 | 101 | 120 | 44633 |
| jit-chiseled | unknown | 1 | 95 | 95 | 27982 |
| r2r | cached | 29 | 99 | 123.4 | 45409 |
| r2r | unknown | 1 | 98 | 98 | 32659 |
| jit | cached | 29 | 101 | 156 | 44469 |
| jit | unknown | 1 | 121 | 121 | 30158 |
