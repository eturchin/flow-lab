import { Radio } from 'lucide-react'
import type { Distribution, Settings } from '../../types'
import { NumberField, SelectField } from '../ui/Field'

export function SourceEditor({
  settings,
  onChange,
}: {
  settings: Settings
  onChange: (settings: Settings) => void
}) {
  return (
    <section className="editor-section">
      <div className="section-label">
        <Radio size={16} />
        <h3>Источник нагрузки</h3>
        <span>01</span>
      </div>
      <div className="form-grid">
        <NumberField
          label="Интенсивность"
          value={settings.arrivalRate}
          onChange={(arrivalRate) => onChange({ ...settings, arrivalRate })}
          min={0.01}
          max={10000}
          step={1}
          unit="req/s"
        />
        <NumberField
          label="Длительность"
          value={settings.durationMs / 1000}
          onChange={(duration) => onChange({ ...settings, durationMs: duration * 1000 })}
          min={0.1}
          max={3600}
          step={1}
          unit="сек"
        />
        <SelectField
          label="Модель поступления"
          value={settings.arrivalDistribution}
          onChange={(value) =>
            onChange({
              ...settings,
              arrivalDistribution: value as Distribution,
            })
          }
        >
          <option value="Exponential">Пуассоновский поток</option>
          <option value="Fixed">Постоянный интервал</option>
        </SelectField>
        <NumberField
          label="Random seed"
          value={settings.seed}
          onChange={(seed) => onChange({ ...settings, seed })}
          min={-2147483648}
          max={2147483647}
          hint="Один seed → один результат"
        />
      </div>
    </section>
  )
}
