import { useState } from 'react'
import type { Settings, NodeKind } from '../../types'
import { canConnect, graphExample } from '../../lib/graph'

const labels: Record<NodeKind, string> = {
  Service: 'СЕРВИС',
  External: 'ВНЕШНИЙ API',
  Queue: 'БУФЕР',
  Broker: 'БРОКЕР · ОБЩАЯ ОЧЕРЕДЬ',
}

export function GraphCanvas({
  settings,
  selectedNode,
  active,
  onSelect,
  onChange,
}: {
  settings: Settings
  selectedNode: string
  active: boolean
  onSelect: (id: string) => void
  onChange: (settings: Settings) => void
}) {
  const [from, setFrom] = useState('source')
  const [to, setTo] = useState('')
  const edges = settings.connections ?? []
  const nodes = settings.nodes
  const levels = new Map<string, number>([['source', 0]])
  const indegree = new Map(
    nodes.map((n) => [n.id, edges.filter((e) => e.to === n.id && e.from !== 'source').length]),
  )
  const pending = nodes.filter((n) => indegree.get(n.id) === 0).map((n) => n.id)
  while (pending.length) {
    const id = pending.shift()!
    levels.set(
      id,
      Math.max(0, ...edges.filter((e) => e.to === id).map((e) => levels.get(e.from) ?? 0)) + 1,
    )
    for (const edge of edges.filter((e) => e.from === id)) {
      indegree.set(edge.to, (indegree.get(edge.to) ?? 1) - 1)
      if (indegree.get(edge.to) === 0) pending.push(edge.to)
    }
  }
  const layers: string[][] = [['source']]
  for (const node of nodes) {
    const level = levels.get(node.id) ?? 1
    while (layers.length <= level) layers.push([])
    layers[level].push(node.id)
  }
  layers.push(['sink'])
  const height = Math.max(250, Math.max(...layers.map((l) => l.length)) * 145 + 40)
  const width = layers.length * 210 + 20
  const positions = new Map<string, { x: number; y: number }>()
  layers.forEach((layer, col) =>
    layer.forEach((id, row) =>
      positions.set(id, { x: col * 210 + 20, y: (height - layer.length * 145) / 2 + row * 145 }),
    ),
  )
  const name = (id: string) =>
    id === 'source' ? 'Источник' : (nodes.find((n) => n.id === id)?.name ?? id)
  const visibleEdges = [
    ...edges,
    ...nodes
      .filter((n) => !edges.some((e) => e.from === n.id))
      .map((n) => ({ from: n.id, to: 'sink' })),
  ]
  return (
    <>
      <div className="graph-presets">
        <span>Быстрый пример:</span>
        <button
          disabled={active}
          onClick={() => {
            onChange(graphExample(settings, false))
            onSelect('source')
          }}
        >
          Два API параллельно
        </button>
        <button
          disabled={active}
          onClick={() => {
            onChange(graphExample(settings, true))
            onSelect('source')
          }}
        >
          RabbitMQ + 3 консьюмера
        </button>
        <small>Заменяет текущую схему в редакторе</small>
      </div>
      <div className="graph-viewport" aria-label="Граф сервисов">
        <svg width={width} height={height} className="service-graph">
          <defs>
            <marker
              id="graph-arrow"
              viewBox="0 0 10 10"
              refX="9"
              refY="5"
              markerWidth="6"
              markerHeight="6"
              orient="auto-start-reverse"
            >
              <path d="M 0 0 L 10 5 L 0 10 z" fill="#6b9981" />
            </marker>
          </defs>
          {visibleEdges.map((edge) => {
            const a = positions.get(edge.from),
              b = positions.get(edge.to)
            if (!a || !b) return null
            return (
              <path
                key={`${edge.from}:${edge.to}`}
                data-from={edge.from}
                data-to={edge.to}
                d={`M ${a.x + 160} ${a.y + 60} C ${a.x + 185} ${a.y + 60}, ${b.x - 25} ${b.y + 60}, ${b.x} ${b.y + 60}`}
                fill="none"
                stroke="#6b9981"
                strokeWidth="1.5"
                markerEnd="url(#graph-arrow)"
              />
            )
          })}
          {[...positions].map(([id, pos]) => {
            const node = nodes.find((n) => n.id === id)
            const special = id === 'source' || id === 'sink'
            return (
              <foreignObject key={id} x={pos.x} y={pos.y} width="160" height="130">
                <button
                  className={`graph-node ${node?.kind.toLowerCase() ?? id} ${selectedNode === id ? 'node-selected' : ''}`}
                  data-node-id={id}
                  disabled={id === 'sink'}
                  onClick={() => onSelect(id)}
                >
                  <small>
                    {node ? labels[node.kind] : id === 'source' ? 'ИСТОЧНИК' : 'ЗАВЕРШЕНИЕ'}
                  </small>
                  <strong title={node?.name}>
                    {node?.name ?? (id === 'source' ? 'Входящий поток' : 'Результат')}
                  </strong>
                  <span>
                    {special
                      ? id === 'source'
                        ? `${settings.arrivalRate} req/s`
                        : 'Все активные ветви'
                      : node!.kind === 'Broker'
                        ? `${node!.queueCapacity} сообщений`
                        : node!.kind === 'Queue'
                          ? `${node!.queueCapacity} запросов`
                          : `${node!.workers} workers · ${node!.processingMs} мс`}
                  </span>
                  <em>
                    {id === 'sink'
                      ? 'Обработка завершена'
                      : node?.kind === 'Broker'
                        ? '1 сообщение → 1 консьюмер'
                        : (id === 'source' ? settings.sourceRouting : node?.routing) ===
                            'RoundRobin'
                          ? 'В одну ветвь · по очереди'
                          : 'Во все исходящие ветви'}
                  </em>
                </button>
              </foreignObject>
            )
          })}
        </svg>
      </div>
      <fieldset className="graph-editor" disabled={active}>
        <div className="graph-connect">
          <label>
            Откуда
            <select
              aria-label="Связь: откуда"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            >
              <option value="source">Источник</option>
              {nodes.map((n) => (
                <option key={n.id} value={n.id}>
                  {n.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Куда
            <select aria-label="Связь: куда" value={to} onChange={(e) => setTo(e.target.value)}>
              <option value="">Выберите узел</option>
              {nodes
                .filter((n) => n.id !== from)
                .map((n) => (
                  <option key={n.id} value={n.id}>
                    {n.name}
                  </option>
                ))}
            </select>
          </label>
          <button
            className="button small"
            disabled={
              !canConnect(edges, from, to) ||
              !nodes.some((n) => n.id === to) ||
              (from !== 'source' && !nodes.some((n) => n.id === from))
            }
            onClick={() => onChange({ ...settings, connections: [...edges, { from, to }] })}
          >
            Добавить связь
          </button>
        </div>
        <div className="connection-list">
          {edges.map((edge) => (
            <span key={`${edge.from}:${edge.to}`}>
              {name(edge.from)} → {name(edge.to)}
              <button
                title={`Удалить связь ${name(edge.from)} → ${name(edge.to)}`}
                onClick={() =>
                  onChange({ ...settings, connections: edges.filter((e) => e !== edge) })
                }
              >
                ×
              </button>
            </span>
          ))}
        </div>
        {nodes.some((n) => !edges.some((e) => e.to === n.id)) && (
          <p role="status">
            Есть неподключённые узлы. Соедините их с источником или удалите перед запуском.
          </p>
        )}
      </fieldset>
    </>
  )
}
