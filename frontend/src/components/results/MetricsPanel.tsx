import { Activity, ArrowUpRight, Clock3, Layers3, TriangleAlert } from 'lucide-react'
import type { Result } from '../../types'

import { format } from '../../lib/format'
export function MetricsPanel({ result }: { result: Result | null }) {
  const cards = [
    {
      label: 'Пропускная способность',
      value: result ? format(result.throughput) : '—',
      unit: 'req/s',
      icon: Activity,
      detail: result
        ? `${format(result.succeeded, 0)} успешных запросов`
        : 'Успешные запросы в секунду',
      color: 'teal',
    },
    {
      label: 'Задержка · p95',
      value: result ? format(result.p95) : '—',
      unit: 'мс',
      icon: Clock3,
      detail: result
        ? `p50 ${format(result.p50)} · p99 ${format(result.p99)} мс`
        : '95% запросов быстрее этого значения',
      color: 'violet',
    },
    {
      label: 'Доля ошибок',
      value: result ? format(result.errorPercent, 2) : '—',
      unit: '%',
      icon: TriangleAlert,
      detail: result
        ? `${format(result.failed, 0)} из ${format(result.total, 0)} запросов`
        : 'Неуспешные / все запросы',
      color: 'orange',
    },
    {
      label: 'Средняя длина очереди',
      value: result ? format(result.averageQueue) : '—',
      unit: 'req',
      icon: Layers3,
      detail: result
        ? `Максимум на узле: ${format(result.maxQueue, 0)}`
        : 'Среднее по виртуальному времени',
      color: 'blue',
    },
  ]
  return (
    <div className="metrics-grid">
      {cards.map((card) => (
        <article className={`metric-card ${card.color}`} key={card.label}>
          <div className="metric-label">
            <span>{card.label}</span>
            <card.icon size={17} />
          </div>
          <div className="metric-value">
            {card.value}
            <small>{card.unit}</small>
          </div>
          <div className="metric-detail">
            <span>{card.detail}</span>
            <ArrowUpRight size={14} />
          </div>
        </article>
      ))}
    </div>
  )
}
