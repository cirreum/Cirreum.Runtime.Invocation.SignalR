namespace Microsoft.Extensions.Hosting;

using Cirreum.Invocation;
using Cirreum.Invocation.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions for registering the Cirreum SignalR invocation source.
/// </summary>
public static class HostApplicationBuilderExtensions {

	private sealed class AddSignalRInvocationMarker { }

	/// <summary>
	/// Registers the Cirreum SignalR invocation source — surfaces SignalR Hubs as Cirreum
	/// invocation sources, with full per-method <see cref="IInvocationContext"/> publication,
	/// per-connection <c>IInvocationConnection</c> materialization, <c>IConnectionLifecycle</c>
	/// callback dispatch, and typed-payload server-push through
	/// <c>IInvocationConnection.SendAsync</c>. Binds instances from
	/// <c>Cirreum:Invocation:Providers:SignalR:Instances:*</c>.
	/// </summary>
	/// <param name="builder">The host application builder.</param>
	/// <param name="configure">
	/// Optional callback to register per-instance Hub bindings using the fluent
	/// <see cref="InvocationBuilderExtensions.AddSignalR{THub}(IInvocationBuilder, string)"/>
	/// extension method on <see cref="IInvocationBuilder"/>.
	/// </param>
	/// <returns>The host application builder for chaining.</returns>
	/// <example>
	/// <code>
	/// builder.AddSignalRInvocation(b =&gt; b
	///     .AddSignalR&lt;ChatHub&gt;("chat")
	///     .AddSignalR&lt;NotificationHub&gt;("notifications"));
	/// </code>
	/// </example>
	public static IHostApplicationBuilder AddSignalRInvocation(
		this IHostApplicationBuilder builder,
		Action<IInvocationBuilder>? configure = null) {

		// Marker dedup — provider registration runs once regardless of repeat calls.
		// The configure callback always runs so repeated calls can still add per-instance
		// hub bindings.
		if (!builder.Services.IsMarkerTypeRegistered<AddSignalRInvocationMarker>()) {
			builder.Services.MarkTypeAsRegistered<AddSignalRInvocationMarker>();

			// Bind global HubOptions defaults from the provider's HubOptions sub-section.
			// Properties declared at Cirreum:Invocation:Providers:SignalR:HubOptions
			// (KeepAliveInterval, ClientTimeoutInterval, EnableDetailedErrors, HandshakeTimeout,
			// etc.) apply to every Hub registered in the host. Per-Hub overrides on
			// HubOptions<THub> (bound by AddSignalR<THub> from the per-instance HubOptions
			// sub-section) take precedence — ASP.NET's HubOptionsSetup<THub> handles the merge
			// automatically. Sub-section binding (vs whole-section binding) keeps the Cirreum
			// framework structure (Instances dictionary, etc.) on the provider section root
			// strictly separated from the SignalR-native options surface.
			var providerHubOptionsSection = builder.Configuration.GetSection(
				$"Cirreum:Invocation:Providers:{SignalRInvocationRegistrar.ProviderKey}:HubOptions");
			builder.Services.Configure<HubOptions>(providerHubOptionsSection);

			builder.RegisterInvocationProvider<
				SignalRInvocationRegistrar,
				SignalRInvocationSettings,
				SignalRInvocationInstanceSettings>();
		}

		configure?.Invoke(new InvocationBuilder(builder));
		return builder;
	}

}
