# Cirreum Runtime Invocation SignalR

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Runtime.Invocation.SignalR.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Invocation.SignalR/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Runtime.Invocation.SignalR.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Runtime.Invocation.SignalR/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Runtime.Invocation.SignalR?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Runtime.Invocation.SignalR/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Runtime.Invocation.SignalR?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Runtime.Invocation.SignalR/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Runtime Extensions package for the Cirreum SignalR invocation source.**

## Overview

`Cirreum.Runtime.Invocation.SignalR` is the L5 Runtime Extensions package that surfaces app-facing extension methods for wiring SignalR Hubs into Cirreum's unified `IInvocationContext` seam. It supplies three extension methods, each on the framework type that makes them most discoverable:

- `AddSignalRInvocation()` on `IHostApplicationBuilder` — registers the SignalR invocation source (marker-dedup'd) and opens the `IInvocationBuilder` scope for per-instance Hub bindings.
- `AddSignalR<THub>(instanceKey)` on `IInvocationBuilder` — captures `THub` at the call site, stashes the `(instanceKey, typeof(THub))` pair as a `SignalRHubMapping` in DI for the L3 registrar to resolve.
- `MapSignalRInvocation()` on `IEndpointRouteBuilder` — invokes every `InvocationProviderMapping` whose `ProviderName` matches `SignalRInvocationRegistrar.ProviderKey`, walking enabled instances and mapping each Hub at its configured path.

Apps install this package directly. It transitively pulls the L3 `Cirreum.Invocation.SignalR` (registrar, settings, HubFilter, connection adapter, `IConnectionSender` impl) and the L4 `Cirreum.Runtime.InvocationProvider` (helper, scope object).

## Architectural position

```
L2 Core
  Cirreum.InvocationProvider               ← abstractions: IInvocationContext, registrar base, ...

L3 Infrastructure
  Cirreum.Invocation.SignalR               ← registrar, settings, HubFilter, connection adapter
  Cirreum.Invocation.WebSockets            ← peer for raw WebSockets

L4 Runtime
  Cirreum.Runtime.InvocationProvider       ← IInvocationBuilder scope object, RegisterInvocationProvider helper

L5 Runtime Extensions
  Cirreum.Runtime.Invocation.SignalR       ← THIS PACKAGE — AddSignalRInvocation, AddSignalR<THub>, MapSignalRInvocation
  Cirreum.Runtime.Invocation               ← umbrella (AddInvocation, MapInvocation across all sources)
```

Mirrors the Identity track's `Cirreum.Runtime.Identity.Oidc` shape — same SRP-split, same per-protocol/umbrella relationship, same generic-method-with-key per-instance binding pattern.

## What's in the box

| Extension | Lives on | Role |
|---|---|---|
| `AddSignalRInvocation(this IHostApplicationBuilder, Action<IInvocationBuilder>?)` (`Microsoft.Extensions.Hosting`) | `IHostApplicationBuilder` | Top-level entry point. Marker-dedup'd registration of the SignalR invocation source; opens the `IInvocationBuilder` scope for per-instance Hub bindings. |
| `AddSignalR<THub>(this IInvocationBuilder, string instanceKey)` (`Cirreum.Invocation`) | `IInvocationBuilder` | Per-instance Hub binding. Captures `THub` at the call site; stashes a `SignalRHubMapping` singleton in DI for the L3 registrar to resolve at endpoints-phase time. |
| `MapSignalRInvocation(this IEndpointRouteBuilder)` (`Microsoft.AspNetCore.Builder`) | `IEndpointRouteBuilder` | Endpoints-phase entry point. Resolves SignalR-tagged `InvocationProviderMapping` records and invokes their deferred `Map` closures. Pairs naturally with ASP.NET's built-in `MapHub<THub>()`. |

## How registration works

The `AddSignalRInvocation()` extension does two things:

1. Marker-dedup'd: registers the SignalR invocation source by calling `builder.RegisterInvocationProvider<SignalRInvocationRegistrar, SignalRInvocationSettings, SignalRInvocationInstanceSettings>()` from the L4 helper. The L4 helper:
   - Binds `Cirreum:Invocation:Providers:SignalR` from `IConfiguration` to `SignalRInvocationSettings`.
   - Calls `registrar.Register(...)` — services phase — which calls `services.AddSignalR()`, registers the `InvocationContextHubFilter` against the global `HubOptions`, and registers `SignalRConnectionSender` as the scoped `IConnectionSender` impl.
   - Stashes an `InvocationProviderMapping` in DI capturing the deferred `registrar.Map(...)` closure.
2. Opens the `IInvocationBuilder` scope for the configure callback so apps can chain `AddSignalR<THub>(instanceKey)` calls per Hub-type.

Inside the configure callback, each `AddSignalR<THub>(instanceKey)` call:

1. Captures `THub` as a generic argument at the call site (this is the whole reason the per-instance call needs to be a generic method — `THub` can't come from JSON).
2. Stashes a `SignalRHubMapping(instanceKey, typeof(THub))` singleton in DI.

`MapSignalRInvocation()` resolves all `InvocationProviderMapping` records with `ProviderName == SignalRInvocationRegistrar.ProviderKey` and invokes their `Map` closures. The L3 registrar's `MapSource`:

1. Resolves the `SignalRHubMapping` for each enabled instance from configuration.
2. Dispatches through reflection to `endpoints.MapHub<THub>(settings.Path)` (ASP.NET ships only a generic-only overload).
3. Wires `RequireAuthorization` with `AuthenticationSchemes = settings.Scheme` if a `Scheme` is set.

## Configuration

SignalR exposes three configuration surfaces — Cirreum mirrors each as an explicit, named sub-section. Cirreum framework fields (`Enabled`, `Path`, `Scheme`) live at the instance-section root; SignalR-native options are namespaced under `HubOptions` and `HttpOptions` sub-sections so the three roles never collide.

```json
{
  "Cirreum": {
    "Invocation": {
      "Providers": {
        "SignalR": {

          "HubOptions": {
            "KeepAliveInterval": "00:01:00",
            "ClientTimeoutInterval": "00:02:00",
            "EnableDetailedErrors": false
          },

          "Instances": {
            "chat": {
              "Enabled": true,
              "Path": "/chat",
              "Scheme": "oidc_primary",

              "HubOptions": {
                "MaximumReceiveMessageSize": 65536,
                "MaximumParallelInvocationsPerClient": 4
              },

              "HttpOptions": {
                "Transports": "WebSockets, LongPolling",
                "ApplicationMaxBufferSize": 131072,
                "TransportMaxBufferSize": 131072,
                "LongPolling": {
                  "PollTimeout": "00:01:30"
                },
                "WebSockets": {
                  "CloseTimeout": "00:00:10"
                }
              }
            },
            "notifications": {
              "Enabled": true,
              "Path": "/notifications",
              "Scheme": "oidc_primary"
            }
          }

        }
      }
    }
  }
}
```

| Sub-section | Binds to | Scope |
|---|---|---|
| `Providers:SignalR:HubOptions` | `HubOptions` (global) | Defaults applied to every Hub in the host |
| `Providers:SignalR:Instances:{key}:HubOptions` | `HubOptions<THub>` (per-Hub) | Per-Hub overrides for this instance's `THub` |
| `Providers:SignalR:Instances:{key}:HttpOptions` | `HttpConnectionDispatcherOptions` (per-mapping) | Transport-level config for this instance's `MapHub` endpoint |

Precedence for Hub-level options is `defaults → global HubOptions → per-Hub HubOptions<THub>` — handled by ASP.NET's `HubOptionsSetup<THub>` automatically. `HttpConnectionDispatcherOptions` is per-mapping only — it has no global form in SignalR's design.

The instance key (`"chat"`, `"notifications"`) is what `AddSignalR<THub>(instanceKey)` binds against — match the call-site key to the configured instance name.

`Scheme` references a configured Authorization instance under `Cirreum:Authorization:Providers:*:Instances:{Scheme}`. Optional — leave unset for unauthenticated hubs (rare).

### Why explicit sub-sections

`HubOptions` and `HttpConnectionDispatcherOptions` are different SignalR types configuring different layers (Hub method invocation vs. HTTP connection dispatch). They share zero property names today, but a flat-binding pattern would still leave intent ambiguous to readers and risk silent collisions if Microsoft ever ships overlapping properties. The explicit sub-sections make the layer split visible in the JSON itself and let JSON schemas validate each sub-section strictly.

## One Hub type per instance

`AddSignalR<THub>(instanceKey)` allows each `THub` to be mapped to **exactly one** instance key per host. ASP.NET's `HubOptions<THub>` is per-Hub-type — there is no per-instance bucket — so mapping the same Hub class to multiple instance keys would silently accumulate `HubOptions` overrides across both sections (last write wins per property), which is confusing rather than meaningful.

To expose the same Hub at multiple paths with different settings, subclass it:

```csharp
public sealed class PublicChatHub : ChatHub { }
public sealed class PrivateChatHub : ChatHub { }

builder.AddSignalRInvocation(b => b
    .AddSignalR<PublicChatHub>("public")
    .AddSignalR<PrivateChatHub>("private"));
```

Each subclass gets its own `HubOptions<T>` and its own DI registration; the inherited Hub method bodies are unchanged. Attempting to map the same `THub` twice throws `InvalidOperationException` with a message pointing to this workaround.

## Server-initiated push

Inject `IConnectionSender` from a SignalR Hub method (or any code running inside the SignalR invocation pipeline — including Conductor command/query handlers triggered from a Hub method) to push to the calling client:

```csharp
public sealed class ChatHub(IConnectionSender sender) : Hub {
    public async Task Echo(string text) {
        await sender.SendAsync("Echo", new { text, at = DateTime.UtcNow });
    }
}

// AsyncLocal flows the invocation through Conductor — the same handler
// code can run from HTTP or SignalR; IConnectionSender lights up only
// when there's a long-lived calling connection.
public sealed class GenerateReportHandler(
    IConnectionSender sender) : ICommandHandler<GenerateReportCommand> {

    public async ValueTask<Result> Handle(GenerateReportCommand cmd, CancellationToken ct) {
        await sender.SendAsync("Progress", new { Percent = 0,   Stage = "Loading"   }, ct);
        // ... work ...
        await sender.SendAsync("Progress", new { Percent = 100, Stage = "Done"      }, ct);
        return Result.Success(/* ... */);
    }

}
```

The no-method `SendAsync<T>(payload)` overload uses the runtime type name as the SignalR method-routing convention (e.g. `SendAsync(new ChatMessage(...))` dispatches to client `connection.on("ChatMessage", ...)`); the keyed `SendAsync<T>(method, payload)` overload accepts an explicit method name.

### What `IConnectionSender` does and doesn't do

`IConnectionSender` is **bound to the active invocation** — it pushes to the connection that delivered the *currently-executing* Hub method (or downstream Conductor handler invoked from one). It is **not** a general server-to-client push mechanism for arbitrary connections.

| You want to | Use |
|---|---|
| Push extra messages to the client that triggered this Hub method (progress, streaming partial results, multi-message responses) | **`IConnectionSender`** (Cirreum-abstracted) |
| Push to a *different* connected client by `ConnectionId` | `IHubContext<THub>.Clients.Client(id).SendAsync(...)` (SignalR-native) |
| Broadcast to all connected clients | `IHubContext<THub>.Clients.All.SendAsync(...)` (SignalR-native) |
| Push to a SignalR group | `IHubContext<THub>.Clients.Group(name).SendAsync(...)` (SignalR-native) |
| Push from a background service, timer, or inbound webhook (no active invocation) | `IHubContext<THub>.Clients.X.SendAsync(...)` (SignalR-native) — `IConnectionSender` would throw because there's no active connection |
| Push from a handler invoked via HTTP | n/a — HTTP is request/response. `IConnectionSender` would throw because `IInvocationContext.Connection` is `null` for HTTP. Use the handler's return value instead. |

`IHubContext<THub>` is registered as a singleton by `services.AddSignalR()` — inject it anywhere, including code with no active invocation context. Cirreum doesn't abstract this surface because "push to arbitrary connection by id / group / all" varies wildly across long-lived transports (SignalR's rich `Clients` API, raw WebSocket's hand-rolled registries, gRPC streaming's one-stream-at-a-time model). The seam unifies *identity, pipeline, and the calling-client reply path*; transport-specific superpowers stay accessible through their native APIs.

### Handlers that may run from both HTTP and SignalR

If you want a Conductor handler to push progress when invoked via SignalR but also work normally when invoked via HTTP, feature-check before pushing:

```csharp
public sealed class GenerateReportHandler(
    IInvocationContextAccessor accessor,
    IConnectionSender sender) : ICommandHandler<GenerateReportCommand> {

    public async ValueTask<Result> Handle(GenerateReportCommand cmd, CancellationToken ct) {
        var canPush = accessor.Current?.Connection is not null;

        if (canPush) await sender.SendAsync("Progress", new { Percent = 0 }, ct);
        // ... work ...
        if (canPush) await sender.SendAsync("Progress", new { Percent = 100 }, ct);

        return Result.Success(/* ... */);
    }

}
```

The HTTP caller gets the return value; the SignalR caller gets the progress stream *and* the return value.

## Connection lifecycle

Implement `IConnectionLifecycle` (from `Cirreum.Invocation.Connections`) and register it in DI to receive `OnConnectedAsync` / `OnDisconnectedAsync` callbacks. The HubFilter dispatches both under a synthetic invocation scope so consumers like `IUserStateAccessor` work normally inside the callbacks. The `DisconnectInfo` parameter on `OnDisconnectedAsync` carries the disconnect circumstances — graceful close vs. abort, the underlying exception (if any), and a human-readable reason — populated by the L3 adapter from SignalR's optional `Exception?` parameter:

```csharp
internal sealed class AuditConnectionLifecycle(ILogger<AuditConnectionLifecycle> logger)
    : IConnectionLifecycle {

    public ValueTask<bool> OnConnectedAsync(IInvocationConnection connection, CancellationToken ct) {
        // Inspect connection.User, connection.ConnectionId, connection.Items, etc.
        // Return false to reject the connection (the upgrade aborts; client sees normal rejection).
        return ValueTask.FromResult(true);
    }

    public ValueTask OnDisconnectedAsync(
        IInvocationConnection connection,
        DisconnectInfo info,
        CancellationToken ct) {

        if (info.WasGraceful) {
            logger.LogInformation("Connection {Id} closed cleanly", connection.ConnectionId);
        } else if (info.Exception is not null) {
            logger.LogWarning(info.Exception,
                "Connection {Id} aborted: {Reason}", connection.ConnectionId, info.Reason);
        }

        return ValueTask.CompletedTask;
    }

}
```

Per-transport mapping for `DisconnectInfo`: SignalR's `Exception?` parameter from `OnDisconnectedAsync(HubLifetimeContext, Exception?)` populates as `WasGraceful = exception is null`, `Exception = exception`, `Reason = exception?.Message`.

## Deployment — works with Azure SignalR Service

**Yes, this package works transparently with [Azure SignalR Service](https://learn.microsoft.com/azure/azure-signalr/).** Azure SignalR Service is a transport-routing layer underneath SignalR — clients connect to Azure's edge, which forwards messages to your app server. Your Hub classes, `IHubFilter`, `IHubContext<THub>`, and Cirreum's invocation seam all use the same public SignalR contracts in either deployment model, so the same Cirreum-wired Hubs run unchanged on either.

The only difference is one extra service registration — the standard Microsoft `AddAzureSignalR()` extension from the [`Microsoft.Azure.SignalR`](https://www.nuget.org/packages/Microsoft.Azure.SignalR) package:

```csharp
var builder = DomainApplication.CreateBuilder(args);

builder.AddSignalRInvocation(b => b
    .AddSignalR<ChatHub>("chat"));

// Wire Azure SignalR Service. Connection string flows through the standard
// IConfiguration chain — Cirreum.Secrets.Azure transparently resolves from
// KeyVault / env vars / user secrets / appsettings.
builder.Services.AddSignalR()
    .AddAzureSignalR(builder.Configuration.GetConnectionString("Azure:SignalR"));

using var app = builder.Build<MyDomainMarker>();
app.UseDefaultMiddleware();
app.MapSignalRInvocation();
await app.RunAsync();
```

Everything else — `InvocationContextHubFilter` publishing `IInvocationContext` per Hub method invocation, `SignalRConnection` materialization, `IConnectionSender` for in-invocation push, `IConnectionLifecycle` for connect/disconnect callbacks, `IHubContext<THub>` for out-of-band push — keeps working identically. The Cirreum framework code doesn't know or care whether SignalR is self-hosted or routed through Azure SignalR Service.

There is intentionally **no separate `Cirreum.Invocation.SignalR.Azure` package**. Azure SignalR Service is a deployment topology, not a different SignalR implementation — the same Cirreum SignalR L3+L5 packages serve both. We add per-implementation Cirreum packages only when there's framework-specific integration code to write (the Authorization track has separate `Oidc` / `Entra` / `External` packages because OIDC and Entra have genuinely different claim shapes, trust roots, and validation paths to abstract). For Azure SignalR Service vs. self-hosted SignalR, Microsoft's own API already abstracts the difference.

## Dependencies

- **Cirreum.Runtime.InvocationProvider** `1.1.0+` — L4 helper (`IInvocationBuilder` scope object, `RegisterInvocationProvider<>` helper, `InvocationProviderMapping` record)
- **Cirreum.Invocation.SignalR** `1.0.1+` — L3 registrar, settings, `HubFilter`, connection adapter, `SignalRInvocationRegistrar.ProviderKey` const
- **Microsoft.AspNetCore.App** (framework reference) — SignalR (`Microsoft.AspNetCore.SignalR`), endpoint routing

## Versioning

Follows [Semantic Versioning](https://semver.org/). Major bumps are coordinated with the L3 `Cirreum.Invocation.SignalR` and the L2 `Cirreum.InvocationProvider` packages.

## License

MIT — see [LICENSE](LICENSE).

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
