import {
  ArrowRight,
  CheckCheck,
  Cloud,
  GitBranch,
  Layers3,
  Plus,
  Radio,
  Server,
} from 'lucide-react'
import type { NodeKind, NodeSettings, Result, Settings } from '../../types'
import { toGraph } from '../../lib/graph'
import { GraphCanvas } from './GraphCanvas'

const kindLabels: Record<NodeKind, string> = {
  Service: 'СЕРВИС',
  Queue: 'ОЧЕРЕДЬ',
  External: 'ВНЕШНИЙ API',
  Broker: 'БРОКЕР',
}
export function TopologyCanvas({
  settings,
  selectedNode,
  result,
  active,
  onSelect,
  onAdd,
  onChange,
}: {
  settings: Settings
  selectedNode: string
  result: Result | null
  active: boolean
  onSelect: (id: string) => void
  onAdd: (kind: NodeKind) => void
  onChange: (settings: Settings) => void
}) {
  const stages: NodeSettings[][] = []
  for (const node of settings.nodes) {
    if (node.parallelWithPrevious && stages.length) stages[stages.length - 1].push(node)
    else stages.push([node])
  }
  function nodeDetail(node: NodeSettings) {
    return node.kind === 'Queue'
      ? `${node.queueCapacity} запросов · ${node.queueDiscipline === 'Lifo' ? 'LIFO' : 'FIFO'}`
      : `${node.workers} workers · ${node.processingMs} мс`
  }
  return (
    <section className="canvas-panel">
      <div className="panel-header">
        <div>
          <GitBranch size={17} />
          <h2>Топология системы</h2>
          <span className="tag">
            {settings.connections != null
              ? 'Граф связей'
              : settings.nodes.some((n) => n.parallelWithPrevious)
                ? 'Параллельные этапы'
                : 'Последовательные этапы'}
          </span>
        </div>
        <div className="canvas-legend">
          <i />
          {active ? 'Симуляция выполняется' : 'Нажмите на узел для настройки'}
        </div>
        {settings.connections == null && (
          <button
            className="button small"
            disabled={active}
            onClick={() => onChange(toGraph(settings))}
          >
            Редактировать связи
          </button>
        )}
      </div>
      <div className={`topology-canvas ${active ? 'simulating' : ''}`}>
        <div className="canvas-coordinate">
          FLOW / {String(settings.nodes.length + 2).padStart(2, '0')} NODES
        </div>
        {settings.connections != null ? (
          <GraphCanvas
            settings={settings}
            selectedNode={selectedNode}
            active={active}
            onSelect={onSelect}
            onChange={onChange}
          />
        ) : (
          <div className="topology-track">
            <button
              className={`flow-node source ${selectedNode === 'source' ? 'node-selected' : ''}`}
              onClick={() => onSelect('source')}
            >
              <div className="node-top">
                <span className="node-icon">
                  <Radio size={20} />
                </span>
                <i />
              </div>
              <small>ИСТОЧНИК</small>
              <strong>Входящий поток</strong>
              <p>{settings.arrivalRate} req/s</p>
              <div className="node-footer">
                {settings.arrivalDistribution === 'Exponential' ? 'Poisson' : 'Constant'}
                <span>→</span>
              </div>
            </button>
            {stages.map((stage, stageIndex) => (
              <div className="flow-segment" key={stage[0].id}>
                <div className="connector">
                  <span />
                  <ArrowRight size={14} />
                </div>
                <div className={stage.length > 1 ? 'parallel-stage' : 'single-stage'}>
                  {stage.length > 1 && (
                    <small className="stage-label">
                      ЭТАП {stageIndex + 1} · ∥ · ЖДЁМ ВСЕ ВЕТВИ
                    </small>
                  )}
                  {stage.map((node) => (
                    <button
                      key={node.id}
                      className={`flow-node ${node.kind.toLowerCase()} ${selectedNode === node.id ? 'node-selected' : ''}`}
                      onClick={() => onSelect(node.id)}
                    >
                      <div className="node-top">
                        <span className="node-icon">
                          {node.kind === 'External' ? (
                            <Cloud size={20} />
                          ) : node.kind === 'Queue' ? (
                            <Layers3 size={20} />
                          ) : (
                            <Server size={20} />
                          )}
                        </span>
                        <i />
                      </div>
                      <small>
                        {kindLabels[node.kind]}
                        {node.parallelWithPrevious ? ' · ПАРАЛЛЕЛЬНО' : ' · НОВЫЙ ЭТАП'}
                      </small>
                      <strong title={node.name}>{node.name}</strong>
                      <p>{nodeDetail(node)}</p>
                      <div className="node-footer">
                        {node.kind === 'Queue'
                          ? 'Buffer'
                          : result?.nodes.find((n) => n.id === node.id)
                            ? `${result.nodes.find((n) => n.id === node.id)!.utilization.toFixed(1)}% загрузка`
                            : 'Готов к запуску'}
                        <span>→</span>
                      </div>
                    </button>
                  ))}
                </div>
              </div>
            ))}
            <div className="flow-segment">
              <div className="connector">
                <span />
                <ArrowRight size={14} />
              </div>
              <div className="flow-node sink">
                <div className="node-top">
                  <span className="node-icon">
                    <CheckCheck size={20} />
                  </span>
                  <i />
                </div>
                <small>ПРИЁМНИК</small>
                <strong>Результат</strong>
                <p>
                  {result
                    ? `${result.succeeded.toLocaleString('ru-RU')} успешно`
                    : 'Завершение запроса'}
                </p>
                <div className="node-footer">
                  Completed<span>✓</span>
                </div>
              </div>
            </div>
          </div>
        )}
        <div className="canvas-bottom">
          <span>
            <i />{' '}
            {settings.connections != null
              ? 'Связи задают маршрут · Брокер распределяет сообщения'
              : '∥ — ветви одного этапа · Ожидание всех ветвей'}{' '}
            · Время в мс
          </span>
          <div className="add-node">
            <Plus size={14} />
            <select
              aria-label="Добавить узел"
              value=""
              disabled={settings.nodes.length >= 32 || active}
              onChange={(event) => {
                if (event.target.value) onAdd(event.target.value as NodeKind)
              }}
            >
              <option value="">Добавить узел</option>
              <option value="Service">Сервис</option>
              <option value="Queue">Очередь</option>
              <option value="External">Внешний API</option>
              <option value="Broker">RabbitMQ / брокер</option>
            </select>
          </div>
        </div>
      </div>
    </section>
  )
}
