import { FlaskConical, X } from 'lucide-react'
import { useEffect, useRef } from 'react'

export function ModelDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    if (open) ref.current?.showModal()
    else ref.current?.close()
  }, [open])
  return (
    <dialog
      ref={ref}
      className="model-dialog"
      onCancel={onClose}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <div className="dialog-heading">
        <span className="eyebrow">
          <FlaskConical size={16} /> МАТЕМАТИЧЕСКАЯ МОДЕЛЬ
        </span>
        <button className="icon-button" onClick={onClose} aria-label="Закрыть">
          <X size={20} />
        </button>
      </div>
      <h2>
        Лаборатория, в которой
        <br />
        время — виртуальное.
      </h2>
      <p>
        FlowLab моделирует граф сервисов, параллельные вызовы и брокеры с консьюмерами: источник →
        сервисы и очереди → приёмник. Это учебная модель для исследования компромиссов, а не точный
        прогноз production-системы.
      </p>
      <ul>
        <li>
          Дискретные события упорядочены по времени и номеру вставки. Все случайные значения зависят
          от seed.
        </li>
        <li>
          Параллельный этап ждёт все ветви. Сервисы используют ограниченные FIFO или LIFO очереди.
          Явный узел очереди задаёт буфер следующего сервиса.
        </li>
        <li>
          Источник останавливается по длительности, принятые запросы дорабатываются. Throughput
          рассчитан с учётом этого времени.
        </li>
        <li>
          Таймаут действует на всю попытку. Повтор начинает маршрут заново. Уже начатая обработка
          продолжает занимать ресурс до завершения.
        </li>
        <li>
          Circuit breaker считает последовательные ошибки обработки узла. В half-open допускается
          одна проба; таймауты клиента и переполнение буфера не считаются ошибками сервиса.
        </li>
        <li>
          Перцентили включают успешные и неуспешные запросы. Загрузка усредняется по обработчикам;
          средняя очередь — по виртуальному времени.
        </li>
        <li>
          Нет сетевого стека, CPU-планировщика, транзакций и побочных эффектов. Повторы реальных
          операций требуют идемпотентности.
        </li>
      </ul>
      <button className="button primary" onClick={onClose}>
        Вернуться в лабораторию
      </button>
    </dialog>
  )
}
