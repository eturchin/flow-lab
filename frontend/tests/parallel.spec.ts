import { expect, test } from '@playwright/test'
import { defaultNode, defaultSettings } from '../src/types'

test('parallel stages persist, run, render and expose the guide', async ({ page, request }) => {
  const settings = {
    ...defaultSettings(),
    arrivalRate: 1,
    arrivalDistribution: 'Fixed',
    durationMs: 2000,
    nodes: [
      {
        ...defaultNode('Service'),
        name: 'Branch A',
        processingMs: 40,
        distribution: 'Fixed',
        errorProbability: 0,
      },
      {
        ...defaultNode('Service'),
        name: 'Branch B',
        processingMs: 70,
        distribution: 'Fixed',
        errorProbability: 0,
        parallelWithPrevious: true,
        queueDiscipline: 'Lifo',
      },
    ],
  }
  const created = await request.post('/api/scenarios', {
    data: { name: 'Parallel integration test', description: '', settings },
  })
  expect(created.status()).toBe(201)
  const scenario = await created.json()
  try {
    const loaded = await (await request.get(`/api/scenarios/${scenario.id}`)).json()
    expect(loaded.settings.nodes[1].parallelWithPrevious).toBe(true)
    expect(loaded.settings.nodes[1].queueDiscipline).toBe('Lifo')
    const started = await request.post(`/api/scenarios/${scenario.id}/runs`)
    expect(started.status()).toBe(202)
    const run = await started.json()
    await expect
      .poll(async () => (await (await request.get(`/api/runs/${run.id}`)).json()).status)
      .toBe('Completed')
    const finished = await (await request.get(`/api/runs/${run.id}`)).json()
    expect(finished.result.p95).toBe(70)
    expect(finished.result.succeeded).toBe(1)
    await page.goto('/')
    await page.locator('.scenario-link').filter({ hasText: 'Parallel integration test' }).click()
    await expect(page.locator('.parallel-stage')).toBeVisible()
    await expect(page.locator('.parallel-stage .flow-node')).toHaveCount(2)
    await page.getByRole('tab', { name: 'Инструкция и примеры' }).click()
    await expect(page.getByRole('heading', { name: 'Инструкция по FlowLab' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Границы модели' })).toBeVisible()
    await page.setViewportSize({ width: 390, height: 844 })
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.getByRole('tab', { name: 'Лаборатория', exact: true }).click()
    await expect(page.locator('.parallel-stage')).toBeVisible()
  } finally {
    await request.delete(`/api/scenarios/${scenario.id}`)
  }
})
