using Microsoft.AspNetCore.SignalR;
using PedeLocal.Infrastructure.RealTime;

namespace PedeLocal.UnitTests.RealTime;

public record SentMessage(string Group, string Event, object Payload);

/// <summary>Hand-rolled fake for <see cref="IHubContext{PedeLocalHub}"/> — the solution has no
/// mocking library, and SignalR's own interfaces are simple enough to fake directly. Records every
/// group send so tests can assert exactly which group/event/payload went out, and can be told to
/// throw for specific groups to exercise <see cref="SignalRRealtimeNotifier"/>'s resilience.</summary>
public class FakeHubContext : IHubContext<PedeLocalHub>
{
    private readonly List<SentMessage> _sent = [];
    private readonly HashSet<string> _throwingGroups;

    public FakeHubContext(IEnumerable<string>? throwingGroups = null) =>
        _throwingGroups = throwingGroups is null ? [] : [.. throwingGroups];

    public IReadOnlyList<SentMessage> Sent => _sent;

    public IHubClients Clients => new FakeHubClients(_sent, _throwingGroups);

    public IGroupManager Groups => throw new NotSupportedException("Not exercised by SignalRRealtimeNotifier.");

    private class FakeHubClients(List<SentMessage> sent, HashSet<string> throwingGroups) : IHubClients
    {
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => new FakeClientProxy(sent, groupName, throwingGroups.Contains(groupName));
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy OthersInGroup(string groupName) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private class FakeClientProxy(List<SentMessage> sent, string group, bool throws) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (throws)
            {
                throw new InvalidOperationException($"Simulated hub failure sending {method} to {group}.");
            }

            sent.Add(new SentMessage(group, method, args[0]!));
            return Task.CompletedTask;
        }
    }
}
