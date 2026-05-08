# Changelog

All notable changes to **Cirreum.Runtime.Invocation.SignalR** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-05-08

### Added

Initial release of the Cirreum SignalR Runtime Extensions package — the L5 piece that surfaces app-facing extension methods for wiring SignalR Hubs into Cirreum's unified `IInvocationContext` seam. Mirrors the Identity track's `Cirreum.Runtime.Identity.Oidc` shape. Anchored by ADR-0002 (Unified `IInvocationContext` Seam).

**Host application extensions (`Microsoft.Extensions.Hosting` namespace):**

- `AddSignalRInvocation(this IHostApplicationBuilder, Action<IInvocationBuilder>?)` — top-level entry point. Marker-dedup'd: (1) binds the `HubOptions` sub-section under `Cirreum:Invocation:Providers:SignalR:HubOptions` to global `HubOptions` so properties like `KeepAliveInterval`, `ClientTimeoutInterval`, `EnableDetailedErrors`, `HandshakeTimeout` declared at the provider level apply to every Hub in the host; (2) registers the SignalR invocation source via the L4 `RegisterInvocationProvider<>` helper. Then opens the `IInvocationBuilder` scope for per-instance `AddSignalR<THub>` chaining via the optional configure callback.

**Builder extensions (`Cirreum.Invocation` namespace):**

- `AddSignalR<THub>(this IInvocationBuilder, string instanceKey) where THub : Hub` — binds `THub` to the configured SignalR instance identified by `instanceKey`. Three responsibilities:
  1. Stashes the `(instanceKey, typeof(THub))` pair as a `SignalRHubMapping` singleton in DI; the L3 `SignalRInvocationRegistrar.MapSource` resolves these at endpoints-phase time and dispatches through reflection to `endpoints.MapHub<THub>(settings.Path, options => ...)` (the 3-param overload that also configures per-mapping `HttpConnectionDispatcherOptions`).
  2. Binds the instance's `HubOptions` sub-section (`Cirreum:Invocation:Providers:SignalR:Instances:{key}:HubOptions`) to `HubOptions<THub>` so apps can override standard Hub-level options (`HandshakeTimeout`, `KeepAliveInterval`, `MaximumReceiveMessageSize`, `MaximumParallelInvocationsPerClient`, `EnableDetailedErrors`, etc.) explicitly. Cirreum framework fields (`Enabled`, `Path`, `Scheme`) at the instance-section root and the per-mapping `HttpOptions` sub-section never collide with `HubOptions` because each binds from its own named sub-section.
  3. Validates one-Hub-type-per-host: throws `InvalidOperationException` if the same `THub` is mapped twice. ASP.NET's `HubOptions<THub>` is per-Hub-type, so per-instance options overrides would silently accumulate (last write wins per property). The exception message points to the subclass workaround for apps that genuinely need to expose the same Hub at multiple paths/settings.

**Endpoint extensions (`Microsoft.AspNetCore.Builder` namespace):**

- `MapSignalRInvocation(this IEndpointRouteBuilder)` — invokes every `InvocationProviderMapping` whose `ProviderName == SignalRInvocationRegistrar.ProviderKey`, walking enabled instances and mapping each Hub at its configured path. Pairs naturally with ASP.NET's built-in `MapHub<THub>()` for apps that compose mapping per-source.

### Architecture position

This package is the **L5 Runtime Extensions** counterpart to the L3 `Cirreum.Invocation.SignalR` package (registrar, settings, HubFilter, connection adapter) and the L4 `Cirreum.Runtime.InvocationProvider` package (helper + scope object). Apps reference this package directly; both L3 and L4 flow in transitively.

Mirrors the Identity track exactly: `AddOidcIdentity` ↔ `AddSignalRInvocation`, `MapOidcIdentity` ↔ `MapSignalRInvocation`. Per-instance side-input differs by track — Identity uses `AddProvisioner<TProvisioner>(key)` on `IIdentityBuilder` (universal across protocols); Invocation uses `AddSignalR<THub>(key)` on `IInvocationBuilder` (source-specific) because `THub` is a typed handle that can't be bound from configuration JSON.
