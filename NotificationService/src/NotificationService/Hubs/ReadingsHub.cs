namespace NotificationService.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// The hub browsers connect to for "there is new data" signals.
/// <para>
/// It has no members beyond the event name because the traffic is one-way: nothing here is callable
/// from a client, and a client that wants readings asks GraphQLGatewayService for them. Connection
/// counts and durations come from ASP.NET Core's own <c>Microsoft.AspNetCore.Http.Connections</c>
/// meter, so there is nothing to override for the sake of metrics either.
/// </para>
/// </summary>
public sealed class ReadingsHub : Hub
{
    /// <summary>
    /// The client-facing event name. A published contract the React app binds to, so it is spelled
    /// once here rather than at the call site.
    /// </summary>
    public const string ReadingsChangedEvent = "readingsChanged";
}
