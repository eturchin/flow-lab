import { ArrowDown, ArrowUp, Server, Trash2 } from 'lucide-react'
import type { Distribution, NodeSettings } from '../../types'
import { NumberField, SelectField, Toggle } from '../ui/Field'

export function NodeEditor({
  node,
  index,
  total,
  onChange,
  onDelete,
  onMove,
  graph = false,
}: {
  node: NodeSettings
  index: number
  total: number
  onChange: (node: NodeSettings) => void
  onDelete: () => void
  onMove: (direction: number) => void
  graph?: boolean
}) {
  const patch = (values: Partial<NodeSettings>) => onChange({ ...node, ...values })
  return (
    <section className="editor-section">
      <div className="section-label">
        <Server size={16} />
        <h3>Параметры узла</h3>
        <span>{String(index + 2).padStart(2, '0')}</span>
      </div>
      <label className="field">
        <span>Название</span>
        <input
          value={node.name}
          maxLength={80}
          onChange={(event) => patch({ name: event.target.value })}
        />
      </label>
      <div className="node-actions">
        <span>
          {node.kind === 'Broker'
            ? 'Брокер сообщений'
            : node.kind === 'Queue'
              ? 'Буфер запросов'
              : node.kind === 'External'
                ? 'Внешний сервис'
                : 'Сервисный узел'}
        </span>
        {!graph && (
          <>
            <button title="Переместить влево" disabled={index === 0} onClick={() => onMove(-1)}>
              <ArrowUp size={14} />
            </button>
            <button
              title="Переместить вправо"
              disabled={index === total - 1}
              onClick={() => onMove(1)}
            >
              <ArrowDown size={14} />
            </button>
          </>
        )}
        <button title="Удалить узел" className="danger-icon" onClick={onDelete}>
          <Trash2 size={14} />
        </button>
      </div>
      <div className="form-grid">
        <SelectField
          label="Порядок очереди"
          value={node.queueDiscipline ?? 'Fifo'}
          onChange={(value) => patch({ queueDiscipline: value as 'Fifo' | 'Lifo' })}
        >
          <option value="Fifo">FIFO · старые сначала</option>
          <option value="Lifo">LIFO · новые сначала</option>
        </SelectField>
        {node.kind !== 'Queue' && node.kind !== 'Broker' && (
          <>
            <NumberField
              label="Обработчики"
              value={node.workers}
              onChange={(workers) => patch({ workers })}
              min={1}
              max={1024}
              unit="шт"
            />
            <NumberField
              label="Время обработки"
              value={node.processingMs}
              onChange={(processingMs) => patch({ processingMs })}
              min={0.01}
              max={60000}
              step={1}
              unit="мс"
            />
            <SelectField
              label="Распределение"
              value={node.distribution}
              onChange={(value) => patch({ distribution: value as Distribution })}
            >
              <option value="Exponential">Экспоненциальное</option>
              <option value="Fixed">Фиксированное</option>
            </SelectField>
            <NumberField
              label="Вариативность"
              value={node.variability * 100}
              onChange={(value) => patch({ variability: value / 100 })}
              max={100}
              unit="%"
            />
            <NumberField
              label="Вероятность ошибки"
              value={Number((node.errorProbability * 100).toFixed(4))}
              onChange={(value) => patch({ errorProbability: value / 100 })}
              max={100}
              step={0.1}
              unit="%"
            />
          </>
        )}
        <NumberField
          label="Ёмкость очереди"
          value={node.queueCapacity}
          onChange={(queueCapacity) => patch({ queueCapacity })}
          max={1000000}
          unit="req"
        />
      </div>
      {graph && node.kind !== 'Broker' && node.kind !== 'Queue' && (
        <SelectField
          label="Исходящие вызовы"
          value={node.routing ?? 'Broadcast'}
          onChange={(value) => patch({ routing: value as 'Broadcast' | 'RoundRobin' })}
        >
          <option value="Broadcast">Все ветви параллельно</option>
          <option value="RoundRobin">Одна ветвь по очереди</option>
        </SelectField>
      )}
      {node.kind === 'Broker' && (
        <p className="graph-node-help">
          Подключите сервисы-консьюмеры связями от брокера. Общая очередь выдаёт каждое сообщение
          одному свободному консьюмеру. Число одновременных доставок определяется обработчиками
          консьюмеров.
        </p>
      )}
      {!graph && node.kind !== 'Queue' && index > 0 && (
        <Toggle
          label="Параллельно с предыдущим сервисом"
          detail="Общий этап ждёт все ветви. После очереди включать нельзя."
          checked={node.parallelWithPrevious ?? false}
          onChange={(parallelWithPrevious) => patch({ parallelWithPrevious })}
        />
      )}
      {node.kind === 'External' && (
        <>
          <h4 className="subheading">Окно деградации</h4>
          <div className="form-grid">
            <NumberField
              label="Начало"
              value={node.outageStartMs / 1000}
              onChange={(value) => patch({ outageStartMs: value * 1000 })}
              unit="сек"
            />
            <NumberField
              label="Длительность"
              value={node.outageDurationMs / 1000}
              onChange={(value) => patch({ outageDurationMs: value * 1000 })}
              unit="сек"
            />
            <NumberField
              label="Множитель задержки"
              value={node.outageLatencyMultiplier}
              onChange={(outageLatencyMultiplier) => patch({ outageLatencyMultiplier })}
              min={1}
              max={100}
              unit="×"
            />
          </div>
          <Toggle
            label="Полный отказ"
            detail="Все запросы в окне завершаются ошибкой"
            checked={node.outageFails}
            onChange={(outageFails) => patch({ outageFails })}
          />
        </>
      )}
    </section>
  )
}
