export async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const timeout = AbortSignal.timeout(30000)
  const signal = options?.signal ? AbortSignal.any([options.signal, timeout]) : timeout
  const headers = new Headers(options?.headers)
  if (!headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      ...options,
      headers,
      signal,
    })
  } catch {
    if (options?.signal?.aborted) throw options.signal.reason
    if (timeout.aborted)
      throw new Error('Сервер не ответил вовремя. Обновите данные перед повтором действия.')
    throw new Error('Сервер недоступен. Проверьте подключение и повторите попытку.')
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new Error(problem.detail || problem.title || `Ошибка запроса (${response.status})`)
  }
  const text = await response.text()
  return text ? (JSON.parse(text) as T) : (undefined as T)
}
