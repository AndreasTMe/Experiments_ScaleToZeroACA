# Images

The four worker images, built from `src/Worker/Dockerfile` with `--target <variant>`. Measured on 2026-11-02, for
`linux/amd64`.

```bash
docker build -f src/Worker/Dockerfile --target aot -t scale-to-zero-worker:aot .
```

## Variants

| Variant        | Code in the image                      | Base image                            | Starts with         |
|----------------|----------------------------------------|---------------------------------------|---------------------|
| `jit`          | IL, framework-dependent                | `runtime:10.0.14-noble`               | `dotnet Worker.dll` |
| `r2r`          | IL plus ReadyToRun code                | `runtime:10.0.14-noble`               | `dotnet Worker.dll` |
| `aot`          | Native AOT binary, no `Worker.dbg`     | `runtime-deps:10.0.14-noble-chiseled` | `/app/Worker`       |
| `jit-chiseled` | IL, framework-dependent, same as `jit` | `runtime:10.0.14-noble-chiseled`      | `dotnet Worker.dll` |

All four are built in `mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot`, publish for `linux-x64`, and run as UID 1654
(`$APP_UID`, the images' `app` user).

## Sizes

| Variant        | Uncompressed | Uncompressed | Compressed (pushed) | Compressed | Layers | Own layers |
|----------------|--------------|--------------|---------------------|------------|--------|------------|
| `jit`          | 208,194,150  | 198.6 MiB    | 86,540,997          | 82.5 MiB   | 7      | 2          |
| `r2r`          | 213,815,422  | 203.9 MiB    | 89,210,726          | 85.1 MiB   | 7      | 2          |
| `aot`          | 23,307,968   | 22.2 MiB     | 10,214,069          | 9.7 MiB    | 4      | 2          |
| `jit-chiseled` | 102,126,739  | 97.4 MiB     | 45,552,966          | 43.4 MiB   | 6      | 2          |

Sizes are in bytes unless marked MiB. The two own layers are `WORKDIR /app` (under 2 KB) and the published app.

| Variant        | App layer, uncompressed | App layer, gzip | Base, uncompressed | Base, compressed |
|----------------|-------------------------|-----------------|--------------------|------------------|
| `jit`          | 5,240,832               | 1,982,155       | 202,987,139        | 84,558,693       |
| `r2r`          | 10,853,376              | 4,651,884       | 202,987,139        | 84,558,693       |
| `aot`          | 9,153,024               | 4,267,650       | 14,157,272         | 5,946,265        |
| `jit-chiseled` | 5,240,832               | 1,982,155       | 96,919,728         | 43,570,656       |

How each column was measured:

- **Uncompressed:** `docker image inspect --format '{{.Size}}'`, on the `overlay2` storage driver (Docker 29.9.2).
- **Compressed (pushed):** the base's layer sizes from its registry manifest for `linux/amd64`, which are exact, plus
  the image's own layers gzipped at level 6, which is what `docker push` does. The own-layer part is an estimate: the
  manifest in the registry after the first push gives the exact figure.
- **Layers:** `RootFS.Layers` in `docker image inspect`. The base layers count toward this too.

## Base images

| Base                                  | Index digest                                                              | `linux/amd64` manifest                                                    |
|---------------------------------------|---------------------------------------------------------------------------|---------------------------------------------------------------------------|
| `runtime:10.0.14-noble`               | `sha256:a3f9f81954b9c810af2ed18849716c8dc74a4707bd0907fdbd69be81554c0f30` | `sha256:de1261f1b137a580e6620fa3f20323efa7bb131a2aeb103cab95077d3445122d` |
| `runtime-deps:10.0.14-noble-chiseled` | `sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296` | `sha256:e70db1cfdcb40055a6715faabebc277a8227dea7c2326a9e9be660251aad40ec` |
| `runtime:10.0.14-noble-chiseled`      | `sha256:3eb54ac708855f74b032de48e02437d15fb083b2aa593d49fdd512bb17f3e944` | `sha256:01734e9e7d221df5412351a35d38b50723fe4ea6bf50813116856cc67163c70b` |
| `sdk:10.0.403-noble-aot` (build only) | `sha256:c45158dabdcffc8409ef46c9431c16b36d9419405b51fd29261fa8a6af3f516f` |                                                                           |

The Dockerfile pins the index digest. All images are under `mcr.microsoft.com/dotnet/`.

## Differences besides the runtime model

The base image defaults were left alone, so these come with each variant:

- The chiseled images set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` and have no ICU. The plain `runtime` image has
  ICU and leaves globalization on. So `jit` and `jit-chiseled` differ in globalization as well as in base size, and so
  do `aot` and `jit`.
- The chiseled images have no shell or package manager, so a container health check can't run a command inside them.
- The plain `runtime` image runs as root unless told otherwise. The Dockerfile sets `USER $APP_UID` in every variant,
  so all four run as UID 1654.

## Verified

- All four built with `docker build --no-cache`, with no warnings, including no trim or AOT warnings for `aot`.
- Each container started against the emulator, logged "Processor started", and answered one trigger from the runner
  with a `done` message. None logged a warning or an exception.
