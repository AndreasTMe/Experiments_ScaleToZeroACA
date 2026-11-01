# Native AOT

Whether the worker builds and starts as a Native AOT binary with the Service Bus SDK, and what the compiler said about
it. Checked on 2026-11-01.

## Result

The publish reports **no trim or AOT warnings**, and the binary works against the emulator with no exceptions. It
started, received a trigger, sent its `done` message and completed the trigger. The JIT worker's `done` message parses
into the same shape, under a parser that rejects missing and unknown fields. This hasn't run against Azure yet.

| Warning code | Member | Package | Count |
|--------------|--------|---------|-------|
| None         |        |         | 0     |

## How it was built

```bash
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -p:TrimmerSingleWarn=false
```

`TrimmerSingleWarn=false` asks for each warning separately, rather than one `IL2104` per assembly. Nothing was added to
the project file, and no warning code was suppressed by this repo. The SDK's own default `NoWarn` does contain `IL2121`,
a warning about unnecessary suppressions.

| Component                    | Version                                                                   |
|------------------------------|---------------------------------------------------------------------------|
| Build image                  | `mcr.microsoft.com/dotnet/sdk:10.0-noble-aot`                             |
| Build image digest           | `sha256:c45158dabdcffc8409ef46c9431c16b36d9419405b51fd29261fa8a6af3f516f` |
| SDK in that image            | 10.0.403                                                                  |
| Runtime and ILCompiler       | 10.0.14                                                                   |
| Azure.Messaging.ServiceBus   | 7.21.0                                                                    |
| Azure.Core                   | 1.62.0                                                                    |
| Microsoft.Extensions.Hosting | 10.0.14                                                                   |
| Image the binary ran on      | `mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble`                     |
| Run image digest             | `sha256:62028293921f5bd6db3d9f9119cd29cd926dfd3c2336da23b09f425e40c3c832` |

`global.json` pins the same SDK, 10.0.403, because the image tag list has no 10.0.1xx tag newer than 10.0.103. The
current binary was built with that pin in place.

## Binary size

| File         | Bytes      | Size     |
|--------------|------------|----------|
| `Worker`     | 9,150,696  | 8.7 MiB  |
| `Worker.dbg` | 16,610,368 | 15.8 MiB |

`Worker.dbg` holds the debug symbols, and is not needed to run the binary.

## Is "no warnings" believable?

The compiler's warnings were on. A control build of a scratch copy added one reflection-based
`JsonSerializer.Serialize` call to `Program.cs`, and reported `IL2026` and `IL3050` from both the analyzer and the AOT
compiler. The real build's log has none of these, and its compiler arguments don't turn the analysis off.

Limits of that result:

- The control only proves warnings surface for the worker's own code. It didn't exercise a warning from inside a
  package.
- A library can silence its own warnings with `UnconditionalSuppressMessage`, and the compiler only analyses code that
  is reachable. So no warnings isn't proof that every path works at runtime.
- The runtime check covers one path: receive, send and complete with a connection string, on the emulator. Completion
  is inferred, because the trigger wasn't redelivered after a restart once its one-minute lock had expired.
