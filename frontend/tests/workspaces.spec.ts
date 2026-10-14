import { expect, test } from '@playwright/test'
import { defaultSettings } from '../src/types'

test('workspaces: create, rename, isolate scenarios, persist and confirm deletions', async ({
  page,
  request,
}) => {
  const name = `Workspace ${Date.now()}`
  let workspaceId: string | undefined
  try {
    await page.goto('/')
    await page.getByTitle('Создать рабочее пространство', { exact: true }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Название пространства').fill(name)
    await dialog.getByRole('button', { name: 'Создать', exact: true }).click()
    await expect(dialog).not.toBeVisible()
    workspaceId = await page.getByLabel('Рабочее пространство', { exact: true }).inputValue()
    await expect(
      page.getByRole('heading', { name: 'Каждая система начинается со сценария' }),
    ).toBeVisible()
    await page.getByRole('button', { name: 'Новый сценарий', exact: true }).click()
    await expect(page.getByLabel('Название сценария')).toBeVisible()
    const scenarioName = await page.getByLabel('Название сценария').inputValue()
    const scenarios = await (await request.get('/api/scenarios')).json()
    const scenario = scenarios.find((s: { workspaceId: string }) => s.workspaceId === workspaceId)
    expect(scenario).toBeTruthy()
    await page.getByLabel('Название сценария').fill('Несохранённое название')
    await page.getByTitle('Переименовать рабочее пространство', { exact: true }).click()
    await dialog.getByLabel('Название пространства').fill(`${name} renamed`)
    await dialog.getByRole('button', { name: 'Сохранить', exact: true }).click()
    await expect(dialog).not.toBeVisible()
    await expect(page.getByLabel('Название сценария')).toHaveValue('Несохранённое название')
    await page.reload()
    await expect(page.getByLabel('Рабочее пространство', { exact: true })).toHaveValue(workspaceId!)
    await expect(page.getByLabel('Рабочее пространство', { exact: true })).toContainText(
      `${name} renamed`,
    )
    await page.getByTitle(`Удалить сценарий «${scenarioName}»`, { exact: true }).click()
    await expect(dialog).toContainText(scenarioName)
    await dialog.getByRole('button', { name: 'Отмена', exact: true }).click()
    expect((await request.get(`/api/scenarios/${scenario.id}`)).ok()).toBeTruthy()
    await page.getByTitle(`Удалить сценарий «${scenarioName}»`, { exact: true }).click()
    await dialog.getByRole('button', { name: 'Удалить сценарий', exact: true }).click()
    await expect(dialog).not.toBeVisible()
    expect((await request.get(`/api/scenarios/${scenario.id}`)).status()).toBe(404)
    // A second scenario checks workspace cascade independently of scenario deletion.
    const created = await request.post('/api/scenarios', {
      data: {
        name: 'Cascade fixture',
        description: '',
        workspaceId,
        settings: { ...defaultSettings(), durationMs: 2000 },
      },
    })
    const child = await created.json()
    const started = await request.post(`/api/scenarios/${child.id}/runs`)
    const run = await started.json()
    await expect
      .poll(async () => (await (await request.get(`/api/runs/${run.id}`)).json()).status)
      .toBe('Completed')
    await page.reload()
    await expect(
      page.locator('.scenario-link').filter({ hasText: 'Cascade fixture' }),
    ).toBeVisible()
    await page.getByTitle('Удалить рабочее пространство', { exact: true }).click()
    await expect(dialog).toContainText(`${name} renamed`)
    await page.keyboard.press('Escape')
    await expect(dialog).not.toBeVisible()
    await page.getByTitle('Удалить рабочее пространство', { exact: true }).click()
    await dialog.getByRole('button', { name: 'Удалить пространство', exact: true }).click()
    await expect(dialog).not.toBeVisible()
    expect((await request.get(`/api/scenarios/${child.id}`)).status()).toBe(404)
    expect((await request.get(`/api/runs/${run.id}`)).status()).toBe(404)
    await page.reload()
    await expect(page.getByLabel('Рабочее пространство', { exact: true })).not.toContainText(name)
    await page.setViewportSize({ width: 390, height: 844 })
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  } finally {
    if (workspaceId) await request.delete(`/api/workspaces/${workspaceId}`)
  }
})

test('workspace API validates names and references', async ({ request }) => {
  expect((await request.post('/api/workspaces', { data: { name: '   ' } })).status()).toBe(400)
  expect(
    (await request.post('/api/workspaces', { data: { name: 'x'.repeat(121) } })).status(),
  ).toBe(400)
  const missing = 'ffffffff-ffff-ffff-ffff-ffffffffffff'
  expect(
    (await request.put(`/api/workspaces/${missing}`, { data: { name: 'Missing' } })).status(),
  ).toBe(404)
  expect((await request.delete(`/api/workspaces/${missing}`)).status()).toBe(404)
  expect(
    (
      await request.post('/api/scenarios', {
        data: {
          name: 'Missing workspace',
          description: '',
          settings: defaultSettings(),
          workspaceId: missing,
        },
      })
    ).status(),
  ).toBe(404)
})

test('workspace deletion refuses active runs without deleting children', async ({ request }) => {
  const workspace = await (
    await request.post('/api/workspaces', { data: { name: 'Active run guard' } })
  ).json()
  let runId: string | undefined
  try {
    const scenario = await (
      await request.post('/api/scenarios', {
        data: {
          name: 'Active scenario',
          description: '',
          workspaceId: workspace.id,
          settings: { ...defaultSettings(), durationMs: 3600000, arrivalRate: 250 },
        },
      })
    ).json()
    const started = await request.post(`/api/scenarios/${scenario.id}/runs`)
    expect(started.status()).toBe(202)
    runId = (await started.json()).id
    const deletion = await request.delete(`/api/workspaces/${workspace.id}`)
    expect(deletion.status()).toBe(409)
    expect((await request.get(`/api/scenarios/${scenario.id}`)).ok()).toBeTruthy()
    expect((await request.get(`/api/runs/${runId}`)).ok()).toBeTruthy()
  } finally {
    if (runId) {
      await request.post(`/api/runs/${runId}/cancel`)
      await expect
        .poll(async () => (await (await request.get(`/api/runs/${runId}`)).json()).status)
        .not.toMatch(/^(Queued|Running)$/)
    }
    expect((await request.delete(`/api/workspaces/${workspace.id}`)).status()).toBe(204)
  }
})
