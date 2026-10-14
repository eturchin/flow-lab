# HTTP API

Все адреса относительно приложения. Формат — JSON camelCase; enum-значения — строки (`Fixed`, `Exponential`, `Service`, `Queue`, `External`). Время — мс, интенсивность — req/s, вероятность — 0…1.

| Метод | Маршрут | Результат |
| --- | --- | --- |
| GET | /api/scenarios | Список ScenarioDto |
| GET | /api/scenarios/{id} | ScenarioDto или 404 |
| POST | /api/scenarios | 201 + ScenarioDto + Location |
| PUT | /api/scenarios/{id} | Обновлённый ScenarioDto |
| DELETE | /api/scenarios/{id} | 204; 409 при активных запусках |
| GET | /api/scenarios/{id}/runs | Последние 100 RunDto, новые сначала |
| POST | /api/scenarios/{id}/runs | 202 + RunDto + Location; 429 при переполнении |
| GET | /api/runs/{id} | Статус, snapshot и результат или 404 |
| GET | /api/runs/{id}/failures?offset=0&limit=50 | Постраничный журнал ошибок; limit 1…200 |
| POST | /api/runs/{id}/cancel | 202, 404 либо 409, если задача завершена |
| GET | /api/runs/compare?baseline={id}&candidate={id} | Метрики двух завершённых запусков одного сценария |
| GET | /health/live | 200, состояние процесса |
| GET | /health/ready | 200 или 503, подключение PostgreSQL |

## Создание сценария

```json
{
  "name": "Order pipeline",
  "description": "Проверка очереди под нагрузкой",
  "settings": {
    "arrivalRate": 80,
    "durationMs": 60000,
    "seed": 42,
    "arrivalDistribution": "Exponential",
    "nodes": [
      {
        "id": "orders",
        "name": "Order Service",
        "kind": "Service",
        "workers": 4,
        "processingMs": 30,
        "distribution": "Exponential",
        "variability": 0,
        "errorProbability": 0.01,
        "queueCapacity": 200
      }
    ],
    "resilience": {
      "timeoutMs": 3000,
      "maxAttempts": 3,
      "retryDelayMs": 100,
      "maxRetryDelayMs": 2000,
      "circuitBreaker": true,
      "failureThreshold": 5,
      "breakDurationMs": 2000
    }
  }
}
```

Параметры внешнего сервиса дополнительно: `outageStartMs`, `outageDurationMs`, `outageLatencyMultiplier`, `outageFails`. Если поле настройки отсутствует, используется значение модели по умолчанию. Неизвестные/некорректные enum и значения отклоняются. После редактирования используйте PUT, затем отдельный POST runs; запуск всегда берёт сохранённую конфигурацию.

## Жизненный цикл

`Queued → Running → Completed | Cancelled | Failed`. У `Completed` заполнен `result`, у Failed/Cancelled — `error`. Частичный результат отменённого запуска не выдаётся как завершённый. Повтор HTTP POST создаёт **новый** эксперимент, а не повторяет тот же id.

## Журнал ошибок

Журнал ошибок возвращается отдельно от основного DTO: `{ total, offset, limit, items }`. Элемент содержит `requestId`, `createdAtMs`, `completedAtMs`, `attempts`, `reason`. Это позволяет разбирать причину каждого неуспешного запроса без загрузки большого журнала вместе с историей. Причины: `QueueFull`, `Timeout`, `ServiceError`, `CircuitOpen`.

## SignalR

Hub: `/hubs/simulation`.

- `Watch(runId)` — подписаться на группу запуска.
- `Unwatch(runId)` — отписаться.
- `Progress`: `{ runId, progress: { percent, total, succeeded, failed, point } }`.
- `Status`: `{ runId, status, progress }`. После терминального уведомления загрузите полный DTO через GET runs/{id}.

`point` содержит `timeMs`, `latencyMs`, `queueLength`, `utilization`, `completed`. Уведомление — оптимизация, а не единственный источник состояния: быстрый запуск может завершиться до подписки, поэтому UI также читает состояние по HTTP.

## Ошибки

Сервер возвращает Problem Details: `status`, `title`, `detail`, `traceId`. 400 — валидация/формат, 404 — отсутствующий ресурс, 409 — конфликт состояния, 429 — переполнение, 503 — недоступная БД, 500 — непредвиденная ошибка. EF-сущности, stack traces и пароль подключения в ответ не включаются.

Сравнение: `absolute = candidate − baseline`, `percent = absolute / baseline × 100`. При 0 → 0 относительное изменение 0; при 0 → ненулевое значение — `null`. Знак улучшения зависит от метрики: рост throughput обычно полезен, рост latency — нет.
