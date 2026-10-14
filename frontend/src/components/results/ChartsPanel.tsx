import { useId, useState } from 'react'
import {
  Area,
  AreaChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { ChartNoAxesCombined, CircleDashed } from 'lucide-react'
import type { Point } from '../../types'
import { format } from '../../lib/format'

const chartOptions = [
  { key: 'latencyMs', title: 'Задержка', unit: 'мс', color: '#11a88e' },
  { key: 'queueLength', title: 'Очередь', unit: 'req', color: '#7b70d8' },
  { key: 'utilization', title: 'Загрузка', unit: '%', color: '#4c92d8' },
] as const
export function ChartsPanel({ points, active }: { points: Point[]; active: boolean }) {
  const [tab, setTab] = useState(0)
  const gradientId = useId().replaceAll(':', '')
  const selected = chartOptions[tab]
  const data = points.map((p) => ({ ...p, seconds: p.timeMs / 1000 }))
  return (
    <section className="chart-panel">
      <div className="panel-header">
        <div>
          <ChartNoAxesCombined size={17} />
          <h2>Поведение системы</h2>
          {active && <span className="live-badge">LIVE</span>}
        </div>
        <div className="chart-tabs">
          {chartOptions.map((option, index) => (
            <button
              className={tab === index ? 'active' : ''}
              key={option.key}
              onClick={() => setTab(index)}
            >
              {option.title}
            </button>
          ))}
        </div>
      </div>
      <div className="chart-caption">
        <span>
          {selected.title}, {selected.unit}
        </span>
        <span>
          <i style={{ background: selected.color }} />
          {tab === 0
            ? 'Среднее по интервалу'
            : tab === 1
              ? 'Суммарная очередь'
              : 'Средняя загрузка сервисов'}
        </span>
      </div>
      <div className="chart-container">
        {data.length === 0 ? (
          <div className="chart-empty">
            <CircleDashed size={32} />
            <strong>Здесь появится поведение вашей системы</strong>
            <span>Настройте сценарий и запустите первую симуляцию</span>
          </div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 12, right: 18, left: -12, bottom: 0 }}>
              <defs>
                <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={selected.color} stopOpacity={0.22} />
                  <stop offset="100%" stopColor={selected.color} stopOpacity={0.01} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 5" vertical={false} stroke="#e9eeee" />
              <XAxis
                dataKey="seconds"
                tickLine={false}
                axisLine={false}
                minTickGap={35}
                tick={{ fontSize: 11, fill: '#8a9994' }}
                tickFormatter={(v) =>
                  `${format(Number(v), (data.at(-1)?.seconds ?? 60) < 10 ? 1 : 0)} с`
                }
              />
              <YAxis
                tickLine={false}
                axisLine={false}
                tick={{ fontSize: 11, fill: '#8a9994' }}
                domain={tab === 2 ? [0, 100] : [0, 'auto']}
              />
              <Tooltip
                labelFormatter={(value) => `${format(Number(value))} сек`}
                formatter={(value) => [`${format(Number(value))} ${selected.unit}`, selected.title]}
                contentStyle={{
                  borderRadius: 10,
                  border: '1px solid #dce7e2',
                  fontSize: 12,
                }}
              />
              <Area
                type="monotone"
                dataKey={selected.key}
                stroke={selected.color}
                strokeWidth={2}
                fill={`url(#${gradientId})`}
                isAnimationActive={false}
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </div>
      <div className="chart-footnote">
        {points.length > 0
          ? `${points.length} временных срезов`
          : '100 временных срезов после завершения'}
        <span>Виртуальное время →</span>
      </div>
    </section>
  )
}
