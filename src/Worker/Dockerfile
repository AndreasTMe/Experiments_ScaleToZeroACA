# One file, five variants: pick one with --target jit, r2r, aot, jit-chiseled or jit-identity. Build from the repo root:
#   docker build -f src/Worker/Dockerfile --target aot -t scale-to-zero-worker:aot .

# The AOT SDK image has the native toolchain Native AOT needs, and builds the other variants just as well.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot@sha256:c45158dabdcffc8409ef46c9431c16b36d9419405b51fd29261fa8a6af3f516f AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Worker/Worker.csproj src/Worker/
RUN dotnet restore src/Worker/Worker.csproj -r linux-x64
COPY src/Worker/ src/Worker/

FROM build AS publish-jit
RUN dotnet publish src/Worker/Worker.csproj -c Release -r linux-x64 --self-contained false -o /app

FROM build AS publish-r2r
RUN dotnet publish src/Worker/Worker.csproj -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o /app

# The build stage restored without Azure.Identity, so this publish restores again with it.
FROM build AS publish-jit-identity
RUN dotnet publish src/Worker/Worker.csproj -c Release -r linux-x64 --self-contained false -p:UseManagedIdentity=true -o /app

FROM build AS publish-aot
RUN dotnet publish src/Worker/Worker.csproj -c Release -r linux-x64 -p:PublishAot=true -p:TrimmerSingleWarn=false -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble@sha256:a3f9f81954b9c810af2ed18849716c8dc74a4707bd0907fdbd69be81554c0f30 AS jit
WORKDIR /app
COPY --from=publish-jit /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble@sha256:a3f9f81954b9c810af2ed18849716c8dc74a4707bd0907fdbd69be81554c0f30 AS r2r
WORKDIR /app
COPY --from=publish-r2r /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble-chiseled@sha256:4309626281bfe550c0f6bb4242c435cd5cdabed1d05a5bc5843f659336f53296 AS aot
WORKDIR /app
# Worker.dbg holds the debug symbols only; the binary runs without it.
COPY --from=publish-aot /app/Worker .
USER $APP_UID
ENTRYPOINT ["/app/Worker"]

FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble-chiseled@sha256:3eb54ac708855f74b032de48e02437d15fb083b2aa593d49fdd512bb17f3e944 AS jit-chiseled
WORKDIR /app
COPY --from=publish-jit /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]

# The jit variant with the managed identity build, on the same base, so the two differ only in how they sign in.
FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble@sha256:a3f9f81954b9c810af2ed18849716c8dc74a4707bd0907fdbd69be81554c0f30 AS jit-identity
WORKDIR /app
COPY --from=publish-jit-identity /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Worker.dll"]
