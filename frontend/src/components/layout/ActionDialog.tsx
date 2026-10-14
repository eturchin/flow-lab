import { useEffect, useRef, useState } from 'react'

export interface DialogAction {
  title: string
  description: string
  confirmLabel: string
  initialName?: string
  onConfirm: (name: string) => Promise<boolean | undefined>
}

export function ActionDialog({
  action,
  onClose,
  error,
}: {
  action: DialogAction
  onClose: () => void
  error: string
}) {
  const ref = useRef<HTMLDialogElement>(null)
  const [name, setName] = useState(action.initialName ?? '')
  const [pending, setPending] = useState(false)
  const [failed, setFailed] = useState(false)
  useEffect(() => {
    ref.current?.showModal()
  }, [])
  return (
    <dialog
      ref={ref}
      className="action-dialog"
      aria-labelledby="action-title"
      aria-describedby="action-description"
      onCancel={(event) => {
        event.preventDefault()
        if (!pending) onClose()
      }}
    >
      {action && (
        <form
          onSubmit={async (event) => {
            event.preventDefault()
            if (pending) return
            setPending(true)
            try {
              const success = await action.onConfirm(name.trim())
              if (success) onClose()
              else setFailed(true)
            } finally {
              setPending(false)
            }
          }}
        >
          <h2 id="action-title">{action.title}</h2>
          <p id="action-description">{action.description}</p>
          {action.initialName !== undefined && (
            <label className="field">
              <span>Название пространства</span>
              <input
                autoFocus
                required
                maxLength={120}
                value={name}
                onChange={(event) => setName(event.target.value)}
                disabled={pending}
              />
            </label>
          )}
          {failed && (
            <p role="alert">{error || 'Не удалось выполнить действие. Повторите попытку.'}</p>
          )}
          <div className="dialog-actions">
            <button
              autoFocus={action.initialName === undefined}
              type="button"
              className="button"
              disabled={pending}
              onClick={onClose}
            >
              Отмена
            </button>
            <button
              className={`button ${action.initialName === undefined ? 'stop' : 'primary'}`}
              disabled={pending || (action.initialName !== undefined && !name.trim())}
              type="submit"
            >
              {pending ? 'Выполняется…' : action.confirmLabel}
            </button>
          </div>
        </form>
      )}
    </dialog>
  )
}
