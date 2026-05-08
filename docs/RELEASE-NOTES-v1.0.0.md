# Cirreum.Runtime.Invocation.SignalR 1.0.0 — App-facing SignalR invocation entry points

Initial release. The L5 Runtime Extensions package that makes SignalR a one-package install for Cirreum apps — bring in this package, write `builder.AddSignalRInvocation(b => b.AddSignalR<ChatHub>("chat"))` in `Program.cs`, and SignalR Hubs flow through Cirreum's unified `IInvocationContext` seam end-to-end.

Anchored by ADR-0002 (Unified `IInvocationContext` Seam). Release #13 in the Invocation family rollout.

---

## Why this release exists

The L3 `Cirreum.Invocation.SignalR 1.0.0` shipped the registrar, settings, HubFilter, connection adapter, and `IConnectionSender` impl. The L4 `Cirreum.Runtime.InvocationProvider 1.1.0` shipped the `RegisterInvocationProvider<>` helper and the `IInvocationBuilder` scope object. Neither alone gives apps a discoverable, ergonomic call site — that's this package's job.

This is the structural mirror of `Cirreum.Runtime.Identity.Oidc` for the Identity track. It surfaces three extension methods, each on the framework type that makes them most discoverable, and pulls L3 + L4 in transitively so apps install one package.

---

## What's new

### `AddSignalRInvocation()` — `IHostApplicationBuilder` extension

```csharp
builder.AddSignalRInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddSignalR<NotificationHub>("notifications"));
```

Top-level entry point. Marker-dedup'd; on first invocation:

1. **Binds the provider's `HubOptions` sub-section to global `HubOptions`.** Properties declared at `Cirreum:Invocation:Providers:SignalR:HubOptions` (`KeepAliveInterval`, `ClientTimeoutInterval`, `EnableDetailedErrors`, `HandshakeTimeout`, `MaximumParallelInvocationsPerClient`, etc.) apply as defaults to every Hub registered in the host. Per-Hub overrides on `HubOptions<THub>` (bound by `AddSignalR<THub>` from each instance's `HubOptions` sub-section) take precedence — ASP.NET's `HubOptionsSetup<THub>` copies global onto per-Hub for unset properties, so the precedence chain is `defaults → global → per-Hub` with no manual merge logic on our side. The explicit sub-section keeps the SignalR-native option surface strictly separated from Cirreum framework structure (`Instances` dictionary at the provider-section root).
2. **Registers the SignalR invocation source** via the L4 `RegisterInvocationProvider<>` helper. The L4 helper binds the per-instance config from `Cirreum:Invocation:Providers:SignalR:Instances:*`, runs the L3 `SignalRInvocationRegistrar.RegisterSource` (which calls `services.AddSignalR()`, registers the `InvocationContextHubFilter`, and registers the scoped `IConnectionSender` impl), and stashes the deferred `InvocationProviderMapping` for endpoints-phase mapping.

Then opens the `IInvocationBuilder` scope and invokes the optional configure callback for per-instance `AddSignalR<THub>` chaining. The callback is optional — apps that bind Hubs to instances entirely from configuration (rare, since `THub` can't come from JSON) can call `builder.AddSignalRInvocation()` with no arguments. The standard pattern is to use the callback to chain `AddSignalR<THub>(instanceKey)` calls per Hub-type.

### `AddSignalR<THub>()` — `IInvocationBuilder` extension

```csharp
public static IInvocationBuilder AddSignalR<THub>(
    this IInvocationBuilder builder,
    string instanceKey) where THub : Hub
```

Binds `THub` to the configured SignalR instance identified by `instanceKey`. The `THub` type is captured at the call site as a generic argument — this is the whole reason the per-instance call needs to be a generic method (you can't put `"ChatHub"` in `appsettings.json` and have it materialize as a `Type`).

Three responsibilities:

1. **Stashes** a `SignalRHubMapping(instanceKey, typeof(THub))` singleton in DI. The L3 `SignalRInvocationRegistrar.MapSource` resolves these at endpoints-phase time and dispatches through reflection to `endpoints.MapHub<THub>(settings.Path, options => ...)` — the 3-param overload that also configures per-mapping `HttpConnectionDispatcherOptions` from the instance's `HttpOptions` sub-section.
2. **Binds** the instance's `HubOptions` sub-section to `HubOptions<THub>` via `services.Configure<HubOptions<THub>>(instanceSection.GetSection("HubOptions"))`. Apps override standard Hub-level SignalR options (`HandshakeTimeout`, `KeepAliveInterval`, `MaximumReceiveMessageSize`, `MaximumParallelInvocationsPerClient`, `EnableDetailedErrors`, etc.) under the explicit `HubOptions` sub-section, which keeps them strictly separated from Cirreum framework fields (`Enabled`, `Path`, `Scheme`) at the instance-section root and from the per-mapping `HttpConnectionDispatcherOptions` (`HttpOptions` sub-section, bound at L3).
3. **Validates** one-Hub-type-per-host. Throws `InvalidOperationException` if the same `THub` is mapped twice — ASP.NET's `HubOptions<THub>` is per-Hub-type, so per-instance overrides would silently accumulate (last write wins per property). The exception message points to the subclass workaround:

   ```csharp
   public sealed class PublicChatHub : ChatHub { }
   public sealed class PrivateChatHub : ChatHub { }

   builder.AddSignalRInvocation(b => b
       .AddSignalR<PublicChatHub>("public")
       .AddSignalR<PrivateChatHub>("private"));
   ```

   Each subclass gets its own `HubOptions<T>` and its own DI registration; inherited Hub method bodies are unchanged.

### `MapSignalRInvocation()` — `IEndpointRouteBuilder` extension

```csharp
app.MapSignalRInvocation();
```

Resolves all registered `InvocationProviderMapping` records with `ProviderName == "SignalR"` and invokes their deferred `Map(IEndpointRouteBuilder)` closures. The closures walk the enabled instances from configuration, dispatch through the cached reflection helper to `MapHub<THub>(...)` for each instance, and apply `RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = settings.Scheme })` when a `Scheme` is configured.

Pairs naturally with ASP.NET's built-in `MapHub<THub>()` for apps that compose mapping per-source. Apps using multiple invocation sources may prefer the umbrella `MapInvocation()` from `Cirreum.Runtime.Invocation` (when that package ships) which invokes every registered mapping regardless of source.

---

## Quick start

```csharp
using Cirreum.Runtime;

var builder = DomainApplication.CreateBuilder(args);

builder.AddSignalRInvocation(b => b
    .AddSignalR<ChatHub>("chat")
    .AddSignalR<NotificationHub>("notifications"));

using var app = builder.Build<MyDomainMarker>();
app.UseDefaultMiddleware();             // wires UseInvocationContext for HTTP automatically
app.MapSignalRInvocation();             // maps the SignalR Hubs declared above
await app.RunAsync();
```

Configuration — three explicit sub-sections for the three SignalR option surfaces:

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
                "LongPolling": { "PollTimeout": "00:01:30" }
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
| `Providers:SignalR:Instances:{key}:HttpOptions` | `HttpConnectionDispatcherOptions` (per-mapping) | Transport-level config for this instance's `MapHub` endpoint (Transports, buffer sizes, LongPolling/WebSockets timeouts) |

ASP.NET's `HubOptionsSetup<THub>` handles the Hub-level precedence merge — `defaults → global HubOptions → per-Hub HubOptions<THub>` — automatically. `HttpConnectionDispatcherOptions` is per-mapping only; SignalR's design has no global form for it.

Explicit sub-sections (rather than flat-binding to permissively-skipping option types) keeps the three roles visible in the JSON and lets JSON schemas validate each sub-section strictly.

---

## Server-initiated push and connection lifecycle

`IConnectionSender` is registered as a scoped service by the L3 SignalR registrar. Inject it from any code running inside the SignalR invocation pipeline (typically Hub methods, command handlers, etc.) to push to the calling client without depending on `IHubContext<THub>` directly:

```csharp
public sealed class ChatHub(IConnectionSender sender) : Hub {
    public async Task Echo(string text) {
        await sender.SendAsync("Echo", new { text, at = DateTime.UtcNow });
    }
}
```

`IConnectionLifecycle` implementations registered in DI receive `OnConnectedAsync` / `OnDisconnectedAsync` callbacks under a synthetic invocation scope so consumers like `IUserStateAccessor` work normally. The `DisconnectInfo` parameter on `OnDisconnectedAsync` carries the disconnect circumstances — graceful close vs. abort, the underlying exception (if any), and a human-readable reason — populated by the L3 adapter from SignalR's optional `Exception?` parameter.

See the L3 `Cirreum.Invocation.SignalR` release notes for the full mechanism documentation.

---

## Architecture position

```
L2 Core
  Cirreum.InvocationProvider               1.1.0+

L3 Infrastructure
  Cirreum.Invocation.SignalR               1.0.0+    ← registrar, settings, HubFilter, connection adapter
  Cirreum.Invocation.WebSockets                       (release #12; not shipped)

L4 Runtime
  Cirreum.Runtime.InvocationProvider       1.1.0+    ← IInvocationBuilder, RegisterInvocationProvider helper

L5 Runtime Extensions
  Cirreum.Runtime.Invocation.SignalR       1.0.0     ← THIS PACKAGE
  Cirreum.Runtime.Invocation.WebSockets               (release #15; not shipped)
  Cirreum.Runtime.Invocation                          (umbrella; release #17; not shipped)
```

This is the structural mirror of `Cirreum.Runtime.Identity.Oidc` for the Identity track. The Invocation track now has its first end-to-end source — apps install this package and SignalR Hubs flow through the unified `IInvocationContext` seam alongside HTTP, with full per-method context publication, per-connection materialization, lifecycle callback dispatch, and server-push.

---

## Dependencies

- **`Cirreum.Runtime.InvocationProvider`** `1.1.0+` — L4 helper + scope object
- **`Cirreum.Invocation.SignalR`** `1.0.0+` — L3 registrar + settings + HubFilter + connection adapter
- **`Microsoft.AspNetCore.App`** (framework reference) — SignalR + endpoint routing

---

## Compatibility

- **Initial release.** No prior version, no migration story.
- Stable public surface — the three extension methods (`AddSignalRInvocation`, `AddSignalR<THub>`, `MapSignalRInvocation`) are intended to evolve only through additive minor bumps.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.0.0`.
- [`Cirreum.Invocation.SignalR 1.0.0`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) — L3 registrar this package wires up.
- [`Cirreum.Runtime.InvocationProvider 1.1.0`](https://www.nuget.org/packages/Cirreum.Runtime.InvocationProvider) — L4 helper this package consumes.
- [`Cirreum.InvocationProvider 1.1.0`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 abstractions everything is built on.
