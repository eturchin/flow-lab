import { expect, test } from '@playwright/test'
import { defaultSettings } from '../src/types'
import type { Scenario, Run } from '../src/types'

test('graph editor: convert legacy route, create independent APIs, edit edges, reject cycles and persist routing', async ({
  page,
  request,
}) => {
  const workspace = await (
    await request.post('/api/workspaces', { data: { name: 'Graph editor test' } })
  ).json()
  const created = await request.post('/api/scenarios', {
    data: {
      name: 'Graph fixture',
      description: '',
      workspaceId: workspace.id,
      settings: {
        ...defaultSettings(),
        arrivalDistribution: 'Fixed',
        arrivalRate: 1,
        durationMs: 2000,
      },
    },
  })
  const scenario: Scenario = await created.json()
  try {
    await page.goto('/')
    await expect(page.getByLabel('Рабочее пространство', { exact: true })).toBeEnabled()
    await page.getByLabel('Рабочее пространство', { exact: true }).selectOption(workspace.id)
    await page.getByRole('button', { name: 'Редактировать связи', exact: true }).click()
    await expect(page.locator('.graph-node')).toHaveCount(4)
    await expect(page.locator('.service-graph path[data-from]')).toHaveCount(3)
    await page.getByRole('button', { name: 'Два API параллельно', exact: true }).click()
    const apiA = page.locator('.graph-node').filter({ hasText: 'API A' })
    const apiB = page.locator('.graph-node').filter({ hasText: 'API B' })
    const a = await apiA.getAttribute('data-node-id')
    const b = await apiB.getAttribute('data-node-id')
    expect((await apiA.boundingBox())!.x).toBe((await apiB.boundingBox())!.x)
    expect((await apiA.boundingBox())!.y).not.toBe((await apiB.boundingBox())!.y)
    await page.getByLabel('Связь: откуда').selectOption(a!)
    await page.getByLabel('Связь: куда').selectOption(b!)
    await page.getByRole('button', { name: 'Добавить связь', exact: true }).click()
    await page.getByLabel('Связь: откуда').selectOption(b!)
    await page.getByLabel('Связь: куда').selectOption(a!)
    await expect(page.getByRole('button', { name: 'Добавить связь', exact: true })).toBeDisabled()
    await page.getByTitle('Удалить связь API A → API B', { exact: true }).click()
    await page.getByRole('button', { name: 'Запустить', exact: true }).click()
    await expect(
      page.getByRole('heading', { name: 'Диагностика узлов', exact: true }),
    ).toBeVisible()
    let runs: Run[] = await (await request.get(`/api/scenarios/${scenario.id}/runs`)).json()
    expect(runs[0].result!.p95).toBe(70)
    expect(runs[0].result!.nodes.map((n) => n.started)).toEqual([1, 1])
    await page.locator('.graph-node.source').click()
    await page.getByLabel('Маршрутизация источника').selectOption('RoundRobin')
    await page.getByRole('button', { name: 'Запустить', exact: true }).click()
    await expect(page.locator('.history-panel tbody tr')).toHaveCount(2)
    await expect
      .poll(
        async () =>
          (await (await request.get(`/api/scenarios/${scenario.id}/runs`)).json())[0].status,
      )
      .toBe('Completed')
    runs = await (await request.get(`/api/scenarios/${scenario.id}/runs`)).json()
    expect(runs[0].result!.p95).toBe(40)
    expect(runs[0].result!.nodes.map((n) => n.started)).toEqual([1, 0])
    await page.reload()
    await expect(page.locator('.graph-node')).toHaveCount(4)
    await expect(page.getByLabel('Маршрутизация источника')).toHaveValue('RoundRobin')
    await page.getByLabel('Добавить узел', { exact: true }).selectOption('External')
    await expect(page.locator('.graph-node')).toHaveCount(5)
    await page.getByTitle('Удалить узел', { exact: true }).click()
    await expect(page.locator('.graph-node')).toHaveCount(4)
    await expect(page.locator('.connection-list')).not.toContainText('External API')
    await page.setViewportSize({ width: 390, height: 844 })
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  } finally {
    await request.delete(`/api/workspaces/${workspace.id}`)
  }
})

test('RabbitMQ preset persists common queue and processes each message in one consumer', async ({
  page,
  request,
}) => {
  const workspace = await (
    await request.post('/api/workspaces', { data: { name: 'Broker test' } })
  ).json()
  const scenario = await (
    await request.post('/api/scenarios', {
      data: {
        name: 'Broker fixture',
        description: '',
        workspaceId: workspace.id,
        settings: {
          ...defaultSettings(),
          arrivalDistribution: 'Fixed',
          arrivalRate: 100,
          durationMs: 2000,
        },
      },
    })
  ).json()
  try {
    await page.goto('/')
    await expect(page.getByLabel('Рабочее пространство', { exact: true })).toBeEnabled()
    await page.getByLabel('Рабочее пространство', { exact: true }).selectOption(workspace.id)
    await page.getByRole('button', { name: 'Редактировать связи', exact: true }).click()
    await page.getByRole('button', { name: 'RabbitMQ + 3 консьюмера', exact: true }).click()
    await expect(page.locator('.graph-node.broker')).toContainText('1 сообщение → 1 консьюмер')
    await page.getByRole('button', { name: 'Запустить', exact: true }).click()
    await expect(
      page.getByRole('heading', { name: 'Диагностика узлов', exact: true }),
    ).toBeVisible()
    const runs: Run[] = await (await request.get(`/api/scenarios/${scenario.id}/runs`)).json()
    const result = runs[0].result!
    const consumers = result.nodes.filter((n) => n.name.startsWith('Consumer'))
    expect(consumers).toHaveLength(3)
    expect(result.succeeded + result.failed).toBe(result.total)
    // Some jobs may hit the end-to-end timeout; every actual delivery still goes to one consumer.
    expect(consumers.reduce((sum, n) => sum + n.started!, 0)).toBe(
      result.nodes.find((n) => n.name === 'RabbitMQ')!.started,
    )
    expect(result.nodes.find((n) => n.name === 'RabbitMQ')!.maxQueue).toBeGreaterThan(0)
    expect(consumers.every((n) => n.maxBusy === 1 && n.maxQueue === 0)).toBe(true)
    await expect(
      page.getByRole('columnheader', { name: 'Вызовы / доставки', exact: true }),
    ).toBeVisible()
    await page.screenshot({ path: '../artifacts/rabbitmq-graph.png', fullPage: true })
    await page.reload()
    await expect(page.locator('.graph-node.broker')).toBeVisible()
    await expect(page.locator('.graph-node')).toHaveCount(7)
    const saved: Scenario = await (await request.get(`/api/scenarios/${scenario.id}`)).json()
    const broker = saved.settings.nodes.find((n) => n.kind === 'Broker')!
    expect(saved.settings.connections!.filter((e) => e.from === broker.id)).toHaveLength(3)
  } finally {
    await request.delete(`/api/workspaces/${workspace.id}`)
  }
})
