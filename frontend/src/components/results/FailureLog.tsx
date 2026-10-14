import { useEffect, useState } from 'react'
import { ChevronLeft, ChevronRight, ListFilter, LoaderCircle } from 'lucide-react'
import { api } from '../../api'
import { format } from '../../lib/format'
import { failureReasons } from '../../lib/failureReasons'

interface FailurePage {
  total: number
  offset: number
  limit: number
  items: {
    requestId: number
    createdAtMs: number
    completedAtMs: number
    attempts: number
    reason: string
  }[]
}

const pageSize = 50
export function FailureLog({ runId, total }: { runId: string; total: number }) {
  const [open, setOpen] = useState(false)
  const [offset, setOffset] = useState(0)
  const [page, setPage] = useState<FailurePage | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    if (!open) return
    const controller = new AbortController()
    void api<FailurePage>(`/runs/${runId}/failures?offset=${offset}&limit=${pageSize}`, {
      signal: controller.signal,
    })
      .then((next) => {
        if (!controller.signal.aborted) setPage(next)
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError((e as Error).message)
      })
    return () => controller.abort()
  }, [runId, offset, open])

  if (total === 0) return null
  const loading = !page || page.offset !== offset
  return (
    <div className="failure-log">
      <button
        className="failure-toggle"
        aria-expanded={open}
        onClick={() => {
          setOpen(!open)
          setError('')
        }}
      >
        <ListFilter size={14} />
        {open ? 'Скрыть журнал ошибок' : `Разобрать ошибки (${format(total, 0)})`}
        <ChevronRight size={14} />
      </button>
      {open && (
        <>
          {error ? (
            <div role="alert" className="alert error">
              {error}
            </div>
          ) : loading ? (
            <div className="failure-loading">
              <LoaderCircle size={16} className="spin" />
              Загружаем журнал…
            </div>
          ) : (
            <>
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th>Запрос</th>
                      <th>Поступил, мс</th>
                      <th>Завершён, мс</th>
                      <th>Попытки</th>
                      <th>Причина</th>
                    </tr>
                  </thead>
                  <tbody>
                    {page.items.map((item) => (
                      <tr key={item.requestId}>
                        <td>#{item.requestId}</td>
                        <td>{format(item.createdAtMs)}</td>
                        <td>{format(item.completedAtMs)}</td>
                        <td>{item.attempts}</td>
                        <td>{failureReasons[item.reason] ?? item.reason}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="failure-pagination">
                <span>
                  {offset + 1}–{Math.min(offset + pageSize, total)} из {format(total, 0)}
                </span>
                <button
                  className="icon-button"
                  disabled={offset === 0}
                  title="Предыдущая страница ошибок"
                  onClick={() => {
                    setOffset(Math.max(0, offset - pageSize))
                    setError('')
                  }}
                >
                  <ChevronLeft size={16} />
                </button>
                <button
                  className="icon-button"
                  disabled={offset + pageSize >= total}
                  title="Следующая страница ошибок"
                  onClick={() => {
                    setOffset(offset + pageSize)
                    setError('')
                  }}
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
