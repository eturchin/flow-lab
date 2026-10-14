import type { Status } from '../types'
export const statusLabels: Record<Status, string> = {
  Queued: 'В очереди',
  Running: 'Выполняется',
  Completed: 'Завершён',
  Cancelled: 'Остановлен',
  Failed: 'Ошибка',
}
