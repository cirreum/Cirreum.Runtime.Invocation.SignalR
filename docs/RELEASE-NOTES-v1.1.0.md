# Cirreum.Runtime.Invocation.SignalR 1.1.0 — flow-through bump for the `IConnectionSender` consolidation

Bumps `Cirreum.Invocation.SignalR` dependency to 1.2.0 to flow through the L2 `IConnectionSender` → `IInvocationConnection.SendAsync` consolidation. The L5 surface is unchanged: `AddSignalRInvocation` / `AddSignalR<THub>` / `MapSignalRInvocation` are byte-compatible with 1.0.0.

---

## What changed

- **`Cirreum.Invocation.SignalR` 1.1.0 → 1.2.0** — pulls in `SignalRConnection.SendAsync<T>` (forwards to `ISingleClientProxy.SendAsync`) and the deletion of `SignalRConnectionSender`. See [`Cirreum.Invocation.SignalR 1.2.0` release notes](https://www.nuget.org/packages/Cirreum.Invocation.SignalR).
- **`Cirreum.InvocationProvider`** flows in transitively at 1.3.0 (the new `IInvocationConnection.SendAsync<T>` interface members; `IConnectionSender` deleted). See [`Cirreum.InvocationProvider 1.3.0` release notes](https://www.nuget.org/packages/Cirreum.InvocationProvider).

---

## App-side migration

Only relevant if your app injected `IConnectionSender` directly. Switch to the ambient connection:

```diff
  public sealed class NotifyHandler(
-     IInvocationContextAccessor accessor,
-     IConnectionSender sender) : ICommandHandler<NotifyCommand> {
+     IInvocationContextAccessor accessor) : ICommandHandler<NotifyCommand> {

      public async ValueTask<Result> Handle(NotifyCommand cmd, CancellationToken ct) {
-         await sender.SendAsync("Notification", cmd.Payload, ct);
+         await accessor.Current?.Connection?.SendAsync("Notification", cmd.Payload, ct);
          return Result.Success();
      }
  }
```

Hub method bodies using `Clients.Caller.SendAsync(...)` directly are unaffected — only framework-abstracted cross-cutting code paths need the migration.

---

## Compatibility

- **Source- and binary-compatible** for `AddSignalRInvocation` / `AddSignalR<THub>` / `MapSignalRInvocation` consumers.
- **Source-incompatible** transitively for app code injecting `IConnectionSender` (see migration above).

---

## See also

- `CHANGELOG.md` — condensed change list for `1.1.0`.
- [`Cirreum.InvocationProvider 1.3.0`](https://www.nuget.org/packages/Cirreum.InvocationProvider) — L2 consolidation.
- [`Cirreum.Invocation.SignalR 1.2.0`](https://www.nuget.org/packages/Cirreum.Invocation.SignalR) — L3 adapter update.
