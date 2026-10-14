import { ShieldCheck } from 'lucide-react'
import type { Resilience } from '../../types'
import { NumberField, Toggle } from '../ui/Field'

export function ResilienceEditor({
  value,
  onChange,
}: {
  value: Resilience
  onChange: (value: Resilience) => void
}) {
  const patch = (changes: Partial<Resilience>) => onChange({ ...value, ...changes })
  return (
    <section className="editor-section">
      <div className="section-label">
        <ShieldCheck size={16} />
        <h3>Отказоустойчивость</h3>
        <span>POLICY</span>
      </div>
      <div className="form-grid">
        <NumberField
          label="Таймаут попытки"
          value={value.timeoutMs}
          onChange={(timeoutMs) => patch({ timeoutMs })}
          min={1}
          max={120000}
          unit="мс"
        />
        <NumberField
          label="Максимум попыток"
          value={value.maxAttempts}
          onChange={(maxAttempts) => patch({ maxAttempts })}
          min={1}
          max={10}
          unit="шт"
        />
        <NumberField
          label="Задержка повтора"
          value={value.retryDelayMs}
          onChange={(retryDelayMs) => patch({ retryDelayMs })}
          min={1}
          unit="мс"
        />
        <NumberField
          label="Предел задержки"
          value={value.maxRetryDelayMs}
          onChange={(maxRetryDelayMs) => patch({ maxRetryDelayMs })}
          min={1}
          unit="мс"
        />
      </div>
      <Toggle
        label="Circuit breaker"
        detail="Изоляция нестабильных зависимостей"
        checked={value.circuitBreaker}
        onChange={(circuitBreaker) => patch({ circuitBreaker })}
      />
      {value.circuitBreaker && (
        <div className="form-grid">
          <NumberField
            label="Порог ошибок"
            value={value.failureThreshold}
            onChange={(failureThreshold) => patch({ failureThreshold })}
            min={1}
            max={1000}
          />
          <NumberField
            label="Время открытия"
            value={value.breakDurationMs}
            onChange={(breakDurationMs) => patch({ breakDurationMs })}
            min={1}
            unit="мс"
          />
        </div>
      )}
      <p className="field-help">
        Повтор проходит весь маршрут. Задержка растёт экспоненциально; переполнение очереди
        завершает запрос сразу.
      </p>
    </section>
  )
}
