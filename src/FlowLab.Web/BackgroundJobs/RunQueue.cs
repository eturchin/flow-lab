using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FlowLab.Application;
using FlowLab.Domain;
using FlowLab.Simulation;
using Microsoft.AspNetCore.SignalR;

namespace FlowLab.Web;

public sealed class RunQueue : IRunScheduler
{
    private readonly SemaphoreSlim slots = new(10, 10);
    private readonly Channel<Guid> channel = Channel.CreateBounded<Guid>(10);
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> tokens = new();
    private readonly object cancellationLock = new();
    public ChannelReader<Guid> Reader => channel.Reader;
    public bool TryReserve() => slots.Wait(0);
    public void ReleaseReservation() => slots.Release();
    public void Enqueue(Guid id)
    {
        var source = new CancellationTokenSource();
        if (!tokens.TryAdd(id, source))
        {
            source.Dispose();
            throw new InvalidOperationException("Запуск уже поставлен в очередь.");
        }
        if (channel.Writer.TryWrite(id))
        {
            return;
        }

        if (tokens.TryRemove(id, out var rejected))
        {
            rejected.Dispose();
        }

        throw new InvalidOperationException("Очередь запусков заполнена.");
    }
    public CancellationToken Token(Guid id) => tokens[id].Token;
    public bool Cancel(Guid id)
    {
        lock (cancellationLock)
        {
            if (!tokens.TryGetValue(id, out var cts))
            {
                return false;
            }

            cts.Cancel();
            return true;
        }
    }
    public void Release(Guid id)
    {
        lock (cancellationLock)
        {
            if (tokens.TryRemove(id, out var cts))
            {
                cts.Dispose();
                slots.Release();
            }
        }
    }
}
