using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FlowLab.Application;
using FlowLab.Domain;
using FlowLab.Simulation;
using Microsoft.AspNetCore.SignalR;

namespace FlowLab.Web;

public sealed class SimulationHub : Hub
{
    public Task Watch(Guid runId) => Groups.AddToGroupAsync(Context.ConnectionId, runId.ToString());
    public Task Unwatch(Guid runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, runId.ToString());
}
