# levers-polling-5

30 cold samples. Times are in milliseconds; the median and p90 are interpolated linearly between samples.
The warm medians are from levers-min-1.

## End-to-end

| Variant | Polling (s) | Cooldown (s) | Samples | Ok | Median | p90 | Max | Warm median | Cold − warm median |
|---|---|---|---|---|---|---|---|---|---|
| jit | 5 | 300 | 30 | 30 | 23017 | 24144.5 | 26100 | 10 | 23007 |

Every sample is ok.

## Phase medians

Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; the sum error is the largest gap between one's phases added up and its end-to-end time.

| Variant | polling | scheduling | imagePull | containerStart | dotnetStartup | serviceBusConnection | handling | Sum of medians | Complete samples | Sum error |
|---|---|---|---|---|---|---|---|---|---|---|
| jit | 1974.9 | 18835.3 | 118.5 | 40.7 | 615 | 1402 | 103.8 | 23090.2 | 30 | 0 |

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
| jit | cached | 29 | 120 | 165.2 | 23041 |
| jit | unknown | 1 | 74 | 74 | 20041 |
