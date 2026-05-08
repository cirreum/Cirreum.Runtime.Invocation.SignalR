namespace Cirreum.Invocation;

using Cirreum.Invocation.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions on <see cref="IInvocationBuilder"/> for binding SignalR Hubs
/// to configured invocation-source instances.
/// </summary>
public static class InvocationBuilderExtensions {

	/// <summary>
	/// Binds <typeparamref name="THub"/> to the configured SignalR invocation-source instance
	/// identified by <paramref name="instanceKey"/>. The <typeparamref name="THub"/> type is
	/// captured at the call site and stashed in DI as a <see cref="SignalRHubMapping"/>; the
	/// L3 <c>SignalRInvocationRegistrar.MapSource</c> resolves these mappings at endpoints-phase
	/// time and dispatches through reflection to <c>endpoints.MapHub&lt;THub&gt;(settings.Path)</c>.
	/// </summary>
	/// <typeparam name="THub">The concrete <see cref="Hub"/> type to map.</typeparam>
	/// <param name="builder">The invocation builder (created by <c>AddSignalRInvocation</c>).</param>
	/// <param name="instanceKey">
	/// The SignalR instance key — must match a configured instance under
	/// <c>Cirreum:Invocation:Providers:SignalR:Instances:{instanceKey}</c>.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// builder.AddSignalRInvocation(b =&gt; b
	///     .AddSignalR&lt;ChatHub&gt;("chat")
	///     .AddSignalR&lt;NotificationHub&gt;("notifications"));
	/// </code>
	/// </example>
	public static IInvocationBuilder AddSignalR<THub>(
		this IInvocationBuilder builder,
		string instanceKey) where THub : Hub {

		ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);

		var services = builder.HostBuilder.Services;

		// Validate THub uniqueness across this host. ASP.NET's HubOptions<THub> is
		// per-Hub-type — there's no per-instance options bucket — so registering the
		// same Hub type for two different instance keys would silently accumulate
		// HubOptions config across both sections (last write wins per property), which
		// is confusing rather than meaningful. Apps that genuinely need to expose the
		// same Hub at multiple paths/auth-schemes should subclass to get distinct types.
		var existing = services.FirstOrDefault(d =>
			d.ServiceType == typeof(SignalRHubMapping)
			&& d.ImplementationInstance is SignalRHubMapping m
			&& m.HubType == typeof(THub));

		if (existing is not null) {
			var existingKey = ((SignalRHubMapping)existing.ImplementationInstance!).InstanceKey;
			throw new InvalidOperationException(
				$"SignalR Hub type '{typeof(THub).Name}' is already mapped to instance '{existingKey}'. " +
				$"Each Hub type can be mapped exactly once because HubOptions<THub> is per-Hub-type, " +
				$"and per-instance HubOptions overrides would silently accumulate (last write wins per " +
				$"property). To expose the same Hub at multiple paths with different settings, subclass " +
				$"it (e.g. `public sealed class PublicChatHub : {typeof(THub).Name} {{ }}` and " +
				$"`public sealed class PrivateChatHub : {typeof(THub).Name} {{ }}`) and map the " +
				$"subclasses to separate instance keys.");
		}

		services.AddSingleton(
			new SignalRHubMapping(instanceKey, typeof(THub)));

		// Bind per-Hub HubOptions<THub> from the instance's HubOptions sub-section. Apps
		// override standard SignalR Hub-level properties (HandshakeTimeout, KeepAliveInterval,
		// MaximumReceiveMessageSize, EnableDetailedErrors, etc.) under the explicit HubOptions
		// sub-section keeping them strictly separated from Cirreum framework fields (Enabled,
		// Path, Scheme) at the instance-section root and from per-mapping HttpConnectionDispatcherOptions
		// (HttpOptions sub-section, bound by the L3 SignalRInvocationRegistrar.MapSource).
		var instanceHubOptionsSection = builder.HostBuilder.Configuration.GetSection(
			$"Cirreum:Invocation:Providers:{SignalRInvocationRegistrar.ProviderKey}:Instances:{instanceKey}:HubOptions");
		services.Configure<HubOptions<THub>>(instanceHubOptionsSection);

		return builder;

	}

}