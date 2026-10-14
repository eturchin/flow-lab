export function readWorkspace(): string | null {
  try { return localStorage.getItem('flowlab.workspace') }
  catch { return null }
}

export function writeWorkspace(id: string) {
  try { localStorage.setItem('flowlab.workspace', id) }
  catch { /* Storage can be disabled; workspace data remains on the server. */ }
}
