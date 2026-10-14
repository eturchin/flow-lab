import { expect, test } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { defaultSettings } from '../src/types'
import type { Run, Scenario } from '../src/types'

async function finished(request: APIRequestContext, id: string) {
  await expect
    .poll(async () => (await (await request.get(`/api/runs/${id}`)).json()).status)
    .toBe('Completed')
  return (await (await request.get(`/api/runs/${id}`)).json()) as Run
}

test('API: snapshots, deterministic reruns, comparison and cascading deletion', async ({
  request,
}) => {
  const settings = { ...defaultSettings(), durationMs: 3000, arrivalRate: 30 }
  const created = await request.post('/api/scenarios', {
    data: { name: 'API integration test', description: 'Temporary test fixture', settings },
  })
  expect(created.status()).toBe(201)
  const scenario: Scenario = await created.json()
  try {
    const start = await request.post(`/api/scenarios/${scenario.id}/runs`)
    expect(start.status()).toBe(202)
    const first = await finished(request, (await start.json()).id)
    const second = await finished(
      request,
      (await (await request.post(`/api/scenarios/${scenario.id}/runs`)).json()).id,
    )
    expect(first.result).toEqual(second.result)
    expect(first.result!.total).toBe(first.result!.succeeded + first.result!.failed)
    const journal = await request.get(`/api/runs/${first.id}/failures?offset=0&limit=2`)
    expect(journal.ok()).toBeTruthy()
    const failurePage = await journal.json()
    expect(failurePage.total).toBe(first.result!.failed)
    expect(failurePage.items.length).toBeLessThanOrEqual(2)
    expect((await request.get(`/api/runs/${first.id}/failures?limit=1000`)).status()).toBe(400)
    const update = await request.put(`/api/scenarios/${scenario.id}`, {
      data: { ...scenario, settings: { ...settings, arrivalRate: 70, seed: 99 } },
    })
    expect(update.ok()).toBeTruthy()
    const third = await finished(
      request,
      (await (await request.post(`/api/scenarios/${scenario.id}/runs`)).json()).id,
    )
    expect(third.snapshot.settings.arrivalRate).toBe(70)
    const historic = await (await request.get(`/api/runs/${first.id}`)).json()
    expect(historic.snapshot.settings.arrivalRate).toBe(30)
    expect(historic.snapshot.settings.seed).toBe(42)
    const comparison = await request.get(
      `/api/runs/compare?baseline=${first.id}&candidate=${third.id}`,
    )
    expect(comparison.ok()).toBeTruthy()
    expect((await comparison.json()).metrics).toHaveLength(7)
    const removed = await request.delete(`/api/scenarios/${scenario.id}`)
    expect(removed.status()).toBe(204)
    expect((await request.get(`/api/runs/${first.id}`)).status()).toBe(404)
  } finally {
    await request.delete(`/api/scenarios/${scenario.id}`)
  }
})

test('API: invalid configurations return readable 400 problems', async ({ request }) => {
  const invalid = await request.post('/api/scenarios', {
    data: { name: '', description: '', settings: { ...defaultSettings(), arrivalRate: -1 } },
  })
  expect(invalid.status()).toBe(400)
  expect((await invalid.json()).detail).toContain('Поток')
  const nullSettings = await request.post('/api/scenarios', {
    data: { name: 'Invalid', description: '', settings: null },
  })
  expect(nullSettings.status()).toBe(400)
})

test('API: SignalR sends bounded progress and active jobs can be cancelled', async ({
  request,
  baseURL,
}) => {
  const settings = { ...defaultSettings(), durationMs: 100000, arrivalRate: 10000 }
  const scenario = await (
    await request.post('/api/scenarios', {
      data: { name: 'Cancellation fixture', description: '', settings },
    })
  ).json()
  const connection = new HubConnectionBuilder()
    .withUrl(`${baseURL}/hubs/simulation`)
    .configureLogging(LogLevel.Error)
    .build()
  const updates: unknown[] = []
  connection.on('Progress', (message) => {
    updates.push(message)
  })
  let runId: string | null = null
  try {
    await connection.start()
    const response = await request.post(`/api/scenarios/${scenario.id}/runs`)
    expect(response.status()).toBe(202)
    runId = (await response.json()).id
    await connection.invoke('Watch', runId)
    await expect.poll(() => updates.length).toBeGreaterThan(0)
    const deletion = await request.delete(`/api/scenarios/${scenario.id}`)
    expect(deletion.status()).toBe(409)
    expect((await request.post(`/api/runs/${runId}/cancel`)).status()).toBe(202)
    await expect
      .poll(async () => (await (await request.get(`/api/runs/${runId}`)).json()).status)
      .toBe('Cancelled')
    expect(updates.length).toBeLessThanOrEqual(100)
  } finally {
    if (runId) await request.post(`/api/runs/${runId}/cancel`)
    await connection.stop()
    await request.delete(`/api/scenarios/${scenario.id}`)
  }
})
