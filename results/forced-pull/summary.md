# forced-pull

120 cold samples. Times are in milliseconds; the median and p90 are interpolated linearly between samples.
The warm medians are from baseline-warm.

## End-to-end

| Variant | Polling (s) | Cooldown (s) | Samples | Ok | Median | p90 | Max | Warm median | Cold − warm median |
|---|---|---|---|---|---|---|---|---|---|
| aot | 30 | 300 | 30 | 30 | 42578 | 46721.8 | 53817 | 10 | 42568 |
| r2r | 30 | 300 | 30 | 30 | 43378 | 47696 | 56598 | 12 | 43366 |
| jit | 30 | 300 | 30 | 30 | 45165 | 48751.8 | 55754 | 10.5 | 45154.5 |
| jit-chiseled | 30 | 300 | 30 | 30 | 45410.5 | 49129.9 | 49651 | 6 | 45404.5 |

Every sample is ok.

## Phase medians

Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; the sum error is the largest gap between one's phases added up and its end-to-end time.

| Variant | polling | scheduling | imagePull | containerStart | dotnetStartup | serviceBusConnection | handling | Sum of medians | Complete samples | Sum error |
|---|---|---|---|---|---|---|---|---|---|---|
| aot | 24680.4 | 18488.4 | 89.2 | 31.5 | 97.3 | 126.3 | -9.1 | 43504 | 30 | 0 |
| r2r | 24803.3 | 18675.7 | 74.9 | 37.9 | 420.7 | 921.1 | 76.3 | 45009.9 | 30 | 0 |
| jit | 25036 | 19056.6 | 76.5 | 33.4 | 619.6 | 1399.7 | 105.5 | 46327.3 | 30 | 0 |
| jit-chiseled | 25339.3 | 18736.8 | 73.6 | 32.3 | 614.3 | 1203.2 | 100.3 | 46099.8 | 30 | 0 |

- polling: triggerEnqueued to scaledOut
- scheduling: scaledOut to pullStarted
- imagePull: pullStarted to imagePulled
- containerStart: imagePulled to processStarted
- dotnetStartup: processStarted to hostStarted
- serviceBusConnection: hostStarted to messageReceived
- handling: messageReceived to doneEnqueued

## Image pulls

A first pull is a sample whose image or digest differs from the previous sample's. The first sample of a run is unknown, since the node may have its image from an earlier run. The pull time is the one the ImagePulled event reports. In a forced-pull run each sample's revision started a replica while it activated, which pulled the copy in full before the sample's replica found it cached; the activation row is those full pulls, and its end-to-end median is the samples' with their pull swapped for it.

| Variant | Pull | Samples | Pull median | Pull p90 | End-to-end median |
|---|---|---|---|---|---|
| aot | cached | 30 | 89 | 114 | 42578 |
| aot | activation | 30 | 591 | 703.7 | 43036 |
| r2r | cached | 30 | 76 | 99.1 | 43378 |
| r2r | activation | 30 | 3026.5 | 3628.7 | 46322 |
| jit | cached | 30 | 77 | 111.5 | 45165 |
| jit | activation | 30 | 2630.5 | 2898.2 | 47380.5 |
| jit-chiseled | cached | 30 | 75.5 | 118.8 | 45410.5 |
| jit-chiseled | activation | 30 | 1585.5 | 1859.5 | 46922 |
