export const format = (value: number, digits = 1) =>
  value.toLocaleString('ru-RU', { maximumFractionDigits: digits })
