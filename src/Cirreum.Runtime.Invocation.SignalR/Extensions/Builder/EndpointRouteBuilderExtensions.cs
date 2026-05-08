namespace Microsoft.AspNetCore.Builder;

using Cirreum.Invocation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// App-facing extensions for mapping the Cirreum SignalR invocation-source endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions {

	/// <summary>
	/// Maps all enabled Cirreum SignalR invocation instances declared in configuration.
	/// Invokes each registered <see cref="InvocationProviderMapping"/> whose
	/// <see cref="InvocationProviderMapping.ProviderName"/> matches
	/// <see cref="SignalRInvocationRegistrar.ProviderKey"/>, which dispatches through
	/// reflection to <c>endpoints.MapHub&lt;THub&gt;(settings.Path)</c> for each instance
	/// and applies <c>RequireAuthorization</c> when a <c>Scheme</c> is configured.
	/// </summary>
	/// <param name="endpoints">The endpoint route builder.</param>
	/// <returns>The endpoint route builder for chaining.</returns>
	/// <remarks>
	/// Pairs naturally with ASP.NET's built-in <c>MapHub&lt;THub&gt;()</c> for apps that compose
	/// mapping per-source. Apps using multiple invocation sources may prefer the umbrella
	/// <c>MapInvocation()</c> from <c>Cirreum.Runtime.Invocation</c> which invokes every
	/// registered mapping regardless of source.
	/// </remarks>
	public static IEndpointRouteBuilder MapSignalRInvocation(
		this IEndpointRouteBuilder endpoints) {

		var mappings = endpoints.ServiceProvider.GetServices<InvocationProviderMapping>();
		foreach (var mapping in mappings) {
			if (mapping.ProviderName == SignalRInvocationRegistrar.ProviderKey) {
				mapping.Map(endpoints);
			}
		}

		return endpoints;
	}

}
