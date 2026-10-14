import { defaultNode } from '../types'
import type { Connection, NodeSettings, Settings } from '../types'

export function toGraph(settings: Settings): Settings {
  if (settings.connections != null) return settings
  const stages: { entries: string[]; exits: string[] }[] = []
  const connections: Connection[] = []
  let buffer: string | undefined
  for (const node of settings.nodes) {
    if (node.kind === 'Queue') {
      buffer = node.id
      continue
    }
    if (!node.parallelWithPrevious || !stages.length) stages.push({ entries: [], exits: [] })
    const stage = stages[stages.length - 1]
    stage.entries.push(buffer ?? node.id)
    stage.exits.push(node.id)
    if (buffer) connections.push({ from: buffer, to: node.id })
    buffer = undefined
  }
  let previous = ['source']
  for (const stage of stages) {
    for (const from of previous) for (const to of stage.entries) connections.push({ from, to })
    previous = stage.exits
  }
  return {
    ...settings,
    connections,
    nodes: settings.nodes.map((n) => ({ ...n, parallelWithPrevious: false })),
  }
}

export function canConnect(edges: Connection[], from: string, to: string): boolean {
  if (
    !from ||
    !to ||
    from === to ||
    to === 'source' ||
    edges.some((e) => e.from === from && e.to === to)
  )
    return false
  const seen = new Set<string>()
  const pending = [to]
  while (pending.length) {
    const id = pending.pop()!
    if (id === from) return false
    if (seen.has(id)) continue
    seen.add(id)
    pending.push(...edges.filter((e) => e.from === id).map((e) => e.to))
  }
  return true
}

export function graphExample(settings: Settings, broker: boolean): Settings {
  const make = (name: string, processingMs: number): NodeSettings => ({
    ...defaultNode('Service'),
    name,
    processingMs,
    distribution: 'Fixed',
    errorProbability: 0,
    workers: 1,
  })
  const nodes = broker
    ? [
        make('Producer API', 10),
        { ...defaultNode('Broker'), name: 'RabbitMQ', queueCapacity: 1000 },
        make('Consumer A', 100),
        make('Consumer B', 150),
        make('Consumer C', 200),
      ]
    : [make('API A', 40), make('API B', 70)]
  const connections = broker
    ? [
        { from: 'source', to: nodes[0].id },
        { from: nodes[0].id, to: nodes[1].id },
        ...nodes.slice(2).map((n) => ({ from: nodes[1].id, to: n.id })),
      ]
    : nodes.map((n) => ({ from: 'source', to: n.id }))
  return { ...settings, nodes, connections, sourceRouting: 'Broadcast' }
}
