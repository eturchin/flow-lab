import { expect, test } from '@playwright/test'
import { defaultNode, defaultSettings } from '../src/types'
import type { Run } from '../src/types'

test('API: capacity is bounded, only two simulations execute at a time', async ({ request }) => {
  const settings = {
    ...defaultSettings(),
    durationMs: 100000,
    arrivalRate: 10000,
    nodes: [
      {
        ...defaultNode('Service'),
        workers: 1024,
        processingMs: 0.01,
        distribution: 'Fixed',
        errorProbability: 1,
        queueCapacity: 1000000,
      },
    ],
    resilience: {
      ...defaultSettings().resilience,
      maxAttempts: 10,
      retryDelayMs: 1,
      maxRetryDelayMs: 1,
    },
  }
  const scenario = await (
    await request.post('/api/scenarios', {
      data: { name: 'Bounded queue fixture', description: '', settings },
    })
  ).json()
  let ids: string[] = []
  try {
    const responses = await Promise.all(
      Array.from({ length: 11 }, () => request.post(`/api/scenarios/${scenario.id}/runs`)),
    )
    expect(responses.filter((response) => response.status() === 429)).toHaveLength(1)
    const accepted = responses.filter((response) => response.status() === 202)
    ids = await Promise.all(accepted.map(async (response) => (await response.json()).id))
    expect(ids).toHaveLength(10)
    const history: Run[] = await (await request.get(`/api/scenarios/${scenario.id}/runs`)).json()
    expect(history.filter((run) => run.status === 'Running').length).toBeLessThanOrEqual(2)
    expect(history.filter((run) => run.status === 'Queued').length).toBeGreaterThan(0)
  } finally {
    await Promise.all(ids.map((id) => request.post(`/api/runs/${id}/cancel`)))
    await expect
      .poll(async () => {
        const history: Run[] = await (
          await request.get(`/api/scenarios/${scenario.id}/runs`)
        ).json()
        return history.filter((run) => run.status === 'Queued' || run.status === 'Running').length
      })
      .toBe(0)
    await request.delete(`/api/scenarios/${scenario.id}`)
  }
})
