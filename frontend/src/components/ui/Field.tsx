import type { ReactNode } from 'react'

interface NumberFieldProps {
  label: string
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
  step?: number
  unit?: string
  hint?: string
}
export function NumberField({
  label,
  value,
  onChange,
  min = 0,
  max,
  step = 1,
  unit,
  hint,
}: NumberFieldProps) {
  return (
    <label className="field">
      <span>{label}</span>
      <div className="input-wrap">
        <input
          type="number"
          value={value}
          min={min}
          max={max}
          step={step}
          required
          onChange={(event) => onChange(event.target.value === '' ? 0 : Number(event.target.value))}
        />
        {unit && <em>{unit}</em>}
      </div>
      {hint && <small>{hint}</small>}
    </label>
  )
}
export function SelectField({
  label,
  value,
  onChange,
  children,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  children: ReactNode
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {children}
      </select>
    </label>
  )
}
export function Toggle({
  checked,
  onChange,
  label,
  detail,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label: string
  detail?: string
}) {
  return (
    <label className="toggle-row">
      <span>
        <strong>{label}</strong>
        {detail && <small>{detail}</small>}
      </span>
      <input
        type="checkbox"
        role="switch"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <i aria-hidden="true" />
    </label>
  )
}
