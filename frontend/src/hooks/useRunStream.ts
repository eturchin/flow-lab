import { useEffect, useState } from 'react'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { api } from '../api'
import type { Point, Run } from '../types'

interface ProgressMessage {
  runId: string
  progress: { percent: number; total: number; succeeded: number; failed: number; point: Point }
}

export function useRunStream(
  runId: string | null,
  onRun: (run: Run) => void,
  onError: (message: string) => void,
) {
  const [stream, setStream] = useState<{
    id: string | null
    connected: boolean
    points: Point[]
    percent: number
    latest: ProgressMessage['progress'] | null
  }>({ id: null, connected: false, points: [], percent: 0, latest: null })
  useEffect(() => {
    if (!runId) return
    let disposed = false
    let fetching = false
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/simulation')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Error)
      .build()
    const refresh = async () => {
      if (disposed || fetching) return
      fetching = true
      try {
        const next = await api<Run>(`/runs/${runId}`)
        if (!disposed) onRun(next)
      } catch (e) {
        if (!disposed) onError((e as Error).message)
      } finally {
        fetching = false
      }
    }
    const markConnected = (connected: boolean) => {
      if (!disposed)
        setStream((previous) => ({
          id: runId,
          connected,
          points: previous.id === runId ? previous.points : [],
          percent: previous.id === runId ? previous.percent : 0,
          latest: previous.id === runId ? previous.latest : null,
        }))
    }
    connection.on('Progress', (message: ProgressMessage) => {
      if (disposed || message.runId !== runId) return
      setStream((previous) => ({
        id: runId,
        connected: true,
        points: [...(previous.id === runId ? previous.points : []), message.progress.point].slice(
          -100,
        ),
        percent: message.progress.percent,
        latest: message.progress,
      }))
    })
    connection.on('Status', () => {
      void refresh()
    })
    connection.onreconnecting(() => markConnected(false))
    connection.onreconnected(async () => {
      try {
        await connection.invoke('Watch', runId)
        markConnected(true)
        void refresh()
      } catch {
        markConnected(false)
      }
    })
    connection.onclose(() => markConnected(false))
    void connection
      .start()
      .then(async () => {
        if (disposed) return
        await connection.invoke('Watch', runId)
        markConnected(true)
        await refresh()
      })
      .catch(() => markConnected(false))
    const timer = window.setInterval(() => {
      void refresh()
    }, 1200)
    return () => {
      disposed = true
      window.clearInterval(timer)
      void connection.stop().catch(() => {
        /* HTTP polling is independent of transport shutdown. */
      })
    }
  }, [runId, onRun, onError])
  return {
    connected: stream.id === runId && stream.connected,
    points: stream.id === runId ? stream.points : [],
    percent: stream.id === runId ? stream.percent : 0,
    latest: stream.id === runId ? stream.latest : null,
  }
}
