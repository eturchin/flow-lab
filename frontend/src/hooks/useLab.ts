import { useCallback, useEffect, useRef, useState } from 'react'
import { useRunStream } from './useRunStream'
import { api } from '../api'
import { defaultSettings } from '../types'
import { readWorkspace, writeWorkspace } from '../lib/workspaceStorage'
import type { Comparison, Run, Scenario, Workspace } from '../types'

const isActive = (run: Run | null) => run?.status === 'Running' || run?.status === 'Queued'

export function useLab() {
  const [workspaces, setWorkspaces] = useState<Workspace[]>([])
  const [workspaceId, setWorkspaceId] = useState<string | null>(null)
  const workspaceSelection = useRef<string | null>(readWorkspace())
  useEffect(() => {
    if (workspaceId) writeWorkspace(workspaceId)
  }, [workspaceId])
  const [scenarios, setScenarios] = useState<Scenario[]>([])
  const [draft, setDraft] = useState<Scenario | null>(null)
  const [saved, setSaved] = useState<Scenario | null>(null)
  const [runs, setRuns] = useState<Run[]>([])
  const [run, setRun] = useState<Run | null>(null)
  const currentRun = useRef<string | null>(null)
  const setCurrentRun = useCallback((next: Run | null) => {
    currentRun.current = next?.id ?? null
    setRun(next)
  }, [])
  const [selecting, setSelecting] = useState(false)
  const operationPending = useRef(false)
  const [comparison, setComparison] = useState<Comparison | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const selection = useRef<string | null>(null)
  const dirty = JSON.stringify(draft) !== JSON.stringify(saved)

  const select = useCallback(async (scenario: Scenario) => {
    selection.current = scenario.id
    setSelecting(true)
    setDraft(structuredClone(scenario))
    setSaved(scenario)
    setCurrentRun(null)
    setComparison(null)
    setRuns([])
    setError('')
    setNotice('')
    try {
      const history = await api<Run[]>(`/scenarios/${scenario.id}/runs`)
      if (selection.current !== scenario.id) return
      setRuns(history)
      setCurrentRun(history[0] ?? null)
    } catch (e) {
      if (selection.current === scenario.id) setError((e as Error).message)
    } finally {
      if (selection.current === scenario.id) setSelecting(false)
    }
  }, [setCurrentRun])

  const clearSelection = useCallback(() => {
    selection.current = null
    setDraft(null)
    setSaved(null)
    setCurrentRun(null)
    setSelecting(false)
    setError('')
    setNotice('')
    setRuns([])
    setComparison(null)
  }, [setCurrentRun])

  const hydrate = useCallback(
    async ([spaces, list]: [Workspace[], Scenario[]]) => {
      setWorkspaces(spaces)
      setScenarios(list)
      const current = spaces.find((w) => w.id === workspaceSelection.current) ?? spaces[0]
      workspaceSelection.current = current?.id ?? null
      setWorkspaceId(current?.id ?? null)
      const available = list.filter((s) => s.workspaceId === current?.id)
      const selected = available.find((s) => s.id === selection.current) ?? available[0]
      if (selected) await select(selected)
      else clearSelection()
    },
    [select, clearSelection],
  )

  const load = useCallback(
    () =>
      Promise.all([api<Workspace[]>('/workspaces'), api<Scenario[]>('/scenarios')]).then(hydrate),
    [hydrate],
  )

  const reload = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      await load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [load])

  useEffect(() => {
    let disposed = false
    void Promise.all([api<Workspace[]>('/workspaces'), api<Scenario[]>('/scenarios')])
      .then((data) => {
        if (!disposed) return hydrate(data)
      })
      .catch((e) => { if (!disposed) setError((e as Error).message) })
      .finally(() => { if (!disposed) setLoading(false) })
    return () => {
      disposed = true
    }
  }, [hydrate])

  const onRunUpdate = useCallback((next: Run) => {
    if (next.scenarioId !== selection.current || next.id !== currentRun.current) return
    setCurrentRun(next)
    setRuns((previous) => [next, ...previous.filter((r) => r.id !== next.id)])
    if (next.error) setError(next.error)
  }, [setCurrentRun])
  const stream = useRunStream(isActive(run) ? run!.id : null, onRunUpdate, setError)

  async function perform(action: () => Promise<void>) {
    if (operationPending.current) return false
    operationPending.current = true
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
      return true
    } catch (e) {
      setError((e as Error).message)
      return false
    } finally {
      setBusy(false)
      operationPending.current = false
    }
  }

  async function saveDraft() {
    if (!draft) throw new Error('Выберите сценарий.')
    const result = await api<Scenario>(`/scenarios/${draft.id}`, {
      method: 'PUT',
      body: JSON.stringify(draft),
    })
    setScenarios((previous) => previous.map((s) => (s.id === result.id ? result : s)))
    setDraft(result)
    setSaved(result)
    return result
  }

  return {
    scenarios: scenarios.filter((s) => s.workspaceId === workspaceId),
    workspaces,
    workspaceId,
    selectWorkspace: async (id: string) => {
      workspaceSelection.current = id
      setWorkspaceId(id)
      clearSelection()
      const first = scenarios.find((s) => s.workspaceId === id)
      if (first) await select(first)
    },
    saveWorkspace: (name: string, id?: string) =>
      perform(async () => {
        const space = await api<Workspace>(id ? `/workspaces/${id}` : '/workspaces', {
          method: id ? 'PUT' : 'POST',
          body: JSON.stringify({ name }),
        })
        if (id) setWorkspaces((previous) => previous.map((w) => (w.id === id ? space : w)))
        else {
          workspaceSelection.current = space.id
          await load()
        }
      }),
    removeWorkspace: (id: string) =>
      perform(async () => {
        await api(`/workspaces/${id}`, { method: 'DELETE' })
        await load()
      }),
    draft,
    setDraft,
    saved,
    runs,
    run: run && isActive(run) ? { ...run, progress: Math.max(run.progress, stream.percent) } : run,
    busy: busy || selecting,
    loading,
    dirty,
    error,
    notice,
    connected: stream.connected,
    liveMetrics: stream.latest,
    comparison,
    points: run?.result?.timeline ?? stream.points,
    active: isActive(run),
    select,
    reload,
    clearError: () => setError(''),
    save: () =>
      perform(async () => {
        await saveDraft()
        setNotice('Изменения сохранены')
      }),
    create: () =>
      perform(async () => {
        const created = await api<Scenario>('/scenarios', {
          method: 'POST',
          body: JSON.stringify({
            name: `Новый сценарий ${scenarios.length + 1}`,
            description: 'Исследуйте поведение своей системы под нагрузкой.',
            settings: defaultSettings(),
            workspaceId,
          }),
        })
        setScenarios((previous) => [...previous, created])
        await select(created)
      }),
    remove: (target: Scenario = draft!) =>
      perform(async () => {
        if (!target) return
        await api(`/scenarios/${target.id}`, { method: 'DELETE' })
        const remaining = scenarios.filter((s) => s.id !== target.id)
        setScenarios(remaining)
        if (draft?.id === target.id) {
          clearSelection()
          const first = remaining.find((s) => s.workspaceId === workspaceId)
          if (first) await select(first)
        }
      }),
    reset: () => {
      if (saved) setDraft(structuredClone(saved))
      setNotice('Параметры восстановлены')
    },
    start: () =>
      perform(async () => {
        const scenario = await saveDraft()
        const created = await api<Run>(`/scenarios/${scenario.id}/runs`, {
          method: 'POST',
        })
        setCurrentRun(created)
        setRuns((previous) => [created, ...previous])
        setComparison(null)
      }),
    cancel: () =>
      perform(async () => {
        if (run) {
          await api(`/runs/${run.id}/cancel`, { method: 'POST' })
          setNotice('Запрошена остановка симуляции')
        }
      }),
    viewRun: (next: Run) => {
      setCurrentRun(next)
      setComparison(null)
      setError(next.error ?? '')
    },
    compare: (baseline: string, candidate: string) =>
      perform(async () => {
        setComparison(
          await api<Comparison>(`/runs/compare?baseline=${baseline}&candidate=${candidate}`),
        )
      }),
  }
}
