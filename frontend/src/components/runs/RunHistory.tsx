import { useState } from 'react'
import { ArrowLeftRight, History, Play, Terminal } from 'lucide-react'
import type { Comparison, Run } from '../../types'
import { format } from '../../lib/format'

import { statusLabels } from '../../lib/runStatus'
export function RunHistory({
  runs,
  currentId,
  onView,
  onCompare,
  comparison,
  busy,
}: {
  runs: Run[]
  currentId?: string
  onView: (run: Run) => void
  onCompare: (a: string, b: string) => void
  comparison: Comparison | null
  busy: boolean
}) {
  const [selected, setSelected] = useState<string[]>([])
  const checked = selected.filter((id) => runs.some((r) => r.id === id && r.result))
  function toggle(id: string) {
    setSelected((previous) =>
      previous.includes(id) ? previous.filter((x) => x !== id) : [...previous.slice(-1), id],
    )
  }
  return (
    <section className="history-panel">
      <div className="panel-header">
        <div>
          <History size={17} />
          <h2>История экспериментов</h2>
          <span className="count-badge">{runs.length}</span>
        </div>
        <button
          className="button small"
          disabled={checked.length !== 2 || busy}
          onClick={() => onCompare(checked[0], checked[1])}
        >
          <ArrowLeftRight size={14} /> Сравнить {checked.length > 0 && `(${checked.length}/2)`}
        </button>
      </div>
      {runs.length === 0 ? (
        <div className="history-empty">
          <Terminal size={23} />
          <span>Пока нет запусков. Первый эксперимент — начало хорошей архитектуры.</span>
        </div>
      ) : (
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th aria-label="Выбор для сравнения" />
                <th>Запуск</th>
                <th>Статус</th>
                <th>Нагрузка</th>
                <th>p95</th>
                <th>Ошибки</th>
                <th aria-label="Открыть результат" />
              </tr>
            </thead>
            <tbody>
              {runs.map((run, index) => (
                <tr className={currentId === run.id ? 'current-run' : ''} key={run.id}>
                  <td>
                    <input
                      type="checkbox"
                      aria-label={`Выбрать запуск ${run.id.slice(0, 8)} для сравнения`}
                      checked={checked.includes(run.id)}
                      disabled={!run.result || busy}
                      onChange={() => toggle(run.id)}
                    />
                  </td>
                  <td>
                    <button className="run-link" disabled={busy} onClick={() => onView(run)}>
                      <strong>
                        #{String(runs.length - index).padStart(3, '0')}{' '}
                        <code>{run.id.slice(0, 8)}</code>
                      </strong>
                      <small>
                        {new Date(run.createdAt).toLocaleString('ru-RU', {
                          day: '2-digit',
                          month: 'short',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}{' '}
                        · seed {run.snapshot.settings.seed}
                      </small>
                    </button>
                  </td>
                  <td>
                    <span className={`status-badge ${run.status.toLowerCase()}`}>
                      <i />
                      {statusLabels[run.status]}
                    </span>
                  </td>
                  <td>{format(run.snapshot.settings.arrivalRate)} req/s</td>
                  <td>{run.result ? `${format(run.result.p95)} мс` : '—'}</td>
                  <td>{run.result ? `${format(run.result.errorPercent, 2)}%` : '—'}</td>
                  <td>
                    <button
                      className="icon-button"
                      title="Открыть результат"
                      disabled={busy}
                      onClick={() => onView(run)}
                    >
                      <Play size={13} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {comparison && (
        <div className="comparison-block">
          <h3>
            <ArrowLeftRight size={16} /> Сравнение экспериментов
          </h3>
          <p>
            Базовый: {comparison.baselineId.slice(0, 8)} → кандидат:{' '}
            {comparison.candidateId.slice(0, 8)}
          </p>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Показатель</th>
                  <th>Базовый</th>
                  <th>Кандидат</th>
                  <th>Абс. изменение</th>
                  <th>Изменение, %</th>
                </tr>
              </thead>
              <tbody>
                {comparison.metrics.map((metric) => (
                  <tr key={metric.name}>
                    <td>{metric.name}</td>
                    <td>{format(metric.baseline, 2)}</td>
                    <td>{format(metric.candidate, 2)}</td>
                    <td>
                      {metric.absolute > 0 ? '+' : ''}
                      {format(metric.absolute, 2)}
                    </td>
                    <td>
                      {metric.percent === null
                        ? '— (база = 0)'
                        : `${metric.percent > 0 ? '+' : ''}${format(metric.percent, 2)}%`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </section>
  )
}
