export const failureReasons: Record<string, string> = {
  QueueFull: 'Переполнение очереди',
  Timeout: 'Таймаут',
  ServiceError: 'Ошибка сервиса',
  CircuitOpen: 'Circuit breaker открыт',
}
