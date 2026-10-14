import { Server } from 'lucide-react'
import type { Result } from '../../types'
import { format } from '../../lib/format'
import { FailureLog } from './FailureLog'

const reasons: Record<string, string> = {
  QueueFull: 'Переполнение очереди',
  Timeout: 'Таймаут',
  ServiceError: 'Ошибка сервиса',
  CircuitOpen: 'Circuit breaker открыт',
}
export function NodeResults({ result, runId }: { result: Result; runId: string }) {
  return (
    <section className="node-results">
      <div className="panel-header">
        <div>
          <Server size={17} />
          <h2>Диагностика узлов</h2>
        </div>
        <span className="muted">
          Обработано за {format(result.elapsedMs / 1000)} с виртуального времени
        </span>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Узел</th>
              <th>Загрузка</th>
              <th>Средняя / max очередь</th>
              <th>Max обработчиков</th>
              <th>Вызовы / доставки</th>
              <th>Breaker: open / half / closed</th>
            </tr>
          </thead>
          <tbody>
            {result.nodes.map((node) => (
              <tr key={node.id}>
                <td>
                  <strong>{node.name}</strong>
                </td>
                <td>
                  <div className="utilization-cell">
                    <div>
                      <i
                        style={{
                          width: `${Math.min(100, node.utilization)}%`,
                          background: node.utilization > 85 ? '#e39b52' : '#13a88e',
                        }}
                      />
                    </div>
                    {format(node.utilization)}%
                  </div>
                </td>
                <td>
                  {format(node.averageQueue)} / {node.maxQueue}
                </td>
                <td>{node.maxBusy}</td>
                <td>{node.started ?? '—'}</td>
                <td>
                  {node.breakerOpened} / {node.breakerHalfOpened} / {node.breakerClosed}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="diagnostics-footer">
        <span>
          Повторов: <strong>{format(result.retries, 0)}</strong>
        </span>
        <span>
          Отброшено: <strong>{format(result.dropped, 0)}</strong>
        </span>
        {Object.entries(result.failureReasons).map(([reason, count]) => (
          <span key={reason}>
            {reasons[reason] ?? reason}: <strong>{format(count, 0)}</strong>
          </span>
        ))}
      </div>
      <FailureLog key={runId} runId={runId} total={result.failed} />
    </section>
  )
}
