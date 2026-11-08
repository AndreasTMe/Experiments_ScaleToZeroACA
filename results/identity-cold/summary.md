# identity-cold

60 cold samples. Times are in milliseconds; the median and p90 are interpolated linearly between samples.

## End-to-end

| Variant | Polling (s) | Cooldown (s) | Samples | Ok | Median | p90 | Max |
|---|---|---|---|---|---|---|---|
| jit-identity | 30 | 300 | 30 | 30 | 45620 | 49539.8 | 53736 |
| jit | 30 | 300 | 30 | 30 | 41900 | 46481.1 | 49381 |

Every sample is ok.

## Phase medians

Each phase ends where the next one starts. Medians don't add up the way samples do, so the sum of the medians is close to the median end-to-end time but not equal to it. A complete sample has every phase; the sum error is the largest gap between one's phases added up and its end-to-end time.

| Variant | polling | scheduling | imagePull | containerStart | dotnetStartup | serviceBusConnection | handling | Sum of medians | Complete samples | Sum error |
|---|---|---|---|---|---|---|---|---|---|---|
| jit-identity | 22542.7 | 18634.1 | 121 | 29.1 | 672.5 | 4195.3 | 98.1 | 46292.8 | 30 | 0 |
| jit | 23172.7 | 18720.9 | 107.6 | 33.9 | 591 | 1312.6 | 100.8 | 44039.5 | 30 | 0 |

- polling: triggerEnqueued to scaledOut
- scheduling: scaledOut to pullStarted
- imagePull: pullStarted to imagePulled
- containerStart: imagePulled to processStarted
- dotnetStartup: processStarted to hostStarted
- serviceBusConnection: hostStarted to messageReceived
- handling: messageReceived to doneEnqueued

## Token medians

serviceBusConnection split at the first token, for the variants that sign in with a managed identity. The three phases add up to serviceBusConnection.

| Variant | beforeToken | token | afterToken | Samples |
|---|---|---|---|---|
| jit-identity | 803.4 | 2890.8 | 503 | 30 |

- beforeToken: hostStarted to firstTokenRequested
- token: firstTokenRequested to firstTokenAcquired
- afterToken: firstTokenAcquired to messageReceived

## Image pulls

A first pull is a sample whose image or digest differs from the previous sample's. The first sample of a run is unknown, since the node may have its image from an earlier run. The pull time is the one the ImagePulled event reports. In a forced-pull run each sample's revision started a replica while it activated, which pulled the copy in full before the sample's replica found it cached; the activation row is those full pulls, and its end-to-end median is the samples' with their pull swapped for it.

| Variant | Pull | Samples | Pull median | Pull p90 | End-to-end median |
|---|---|---|---|---|---|
| jit-identity | cached | 29 | 124 | 165.8 | 45291 |
| jit-identity | unknown | 1 | 99 | 99 | 46220 |
| jit | cached | 29 | 110 | 170.2 | 42082 |
| jit | unknown | 1 | 78 | 78 | 39034 |
