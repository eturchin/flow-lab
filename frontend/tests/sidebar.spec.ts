import { expect, test } from '@playwright/test'
import { defaultSettings } from '../src/types'

test('sidebar: only scenario list scrolls, controls stay visible at desktop and mobile sizes', async ({
  page,
  request,
}) => {
  const workspace = await (
    await request.post('/api/workspaces', {
      data: { name: 'Пространство для проверки прокрутки и длинного названия'.repeat(2) },
    })
  ).json()
  const other = await (
    await request.post('/api/workspaces', { data: { name: 'Пустое пространство' } })
  ).json()
  try {
    for (let i = 0; i < 30; i++) {
      const response = await request.post('/api/scenarios', {
        data: {
          name: `Сценарий ${String(i + 1).padStart(2, '0')}`,
          description: '',
          settings: defaultSettings(),
          workspaceId: workspace.id,
        },
      })
      expect(response.status()).toBe(201)
    }
    await page.goto('/')
    const picker = page.getByLabel('Рабочее пространство', { exact: true })
    await expect(picker).toBeEnabled()
    await picker.selectOption(workspace.id)
    const list = page.getByRole('navigation', { name: 'Сценарии', exact: true })
    await expect(list.locator('.scenario-link')).toHaveCount(30)
    for (const viewport of [
      { width: 1440, height: 1000 },
      { width: 1024, height: 640 },
      { width: 800, height: 400 },
      { width: 390, height: 844 },
    ]) {
      await page.setViewportSize(viewport)
      await list.evaluate((el) => {
        el.scrollTop = 0
      })
      const before = await picker.boundingBox()
      expect(await list.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(true)
      await list.hover()
      await page.mouse.wheel(0, 600)
      await expect.poll(() => list.evaluate((el) => el.scrollTop)).toBeGreaterThan(0)
      expect(await picker.boundingBox()).toEqual(before)
      expect(await page.locator('.sidebar').evaluate((el) => el.scrollTop)).toBe(0)
      await expect(
        page.getByRole('button', { name: 'Новый сценарий', exact: true }),
      ).toBeInViewport()
      await expect(
        page.getByTitle('Создать рабочее пространство', { exact: true }),
      ).toBeInViewport()
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(
        true,
      )
      if (viewport.width > 700) {
        const bounds = await page.locator('.sidebar').boundingBox()
        expect(bounds!.height).toBe(viewport.height)
      }
    }
    await list.focus()
    await page.keyboard.press('End')
    await expect(list.locator('.scenario-link').last()).toBeInViewport()
    await list.locator('.scenario-link').last().click()
    await expect(page.getByLabel('Название сценария')).toHaveValue('Сценарий 30')
    await picker.selectOption(other.id)
    await expect(list.locator('.scenario-link')).toHaveCount(0)
    await expect(list.getByText('Пока нет сценариев.', { exact: false })).toBeVisible()
    await picker.selectOption(workspace.id)
    await expect(list.locator('.scenario-link')).toHaveCount(30)
    await page.reload()
    await expect(picker).toHaveValue(workspace.id)
    await page.setViewportSize({ width: 1440, height: 1000 })
    await page.screenshot({ path: '../artifacts/sidebar-desktop.png' })
    await page.setViewportSize({ width: 390, height: 844 })
    await page.screenshot({ path: '../artifacts/sidebar-mobile.png' })
  } finally {
    await request.delete(`/api/workspaces/${workspace.id}`)
    await request.delete(`/api/workspaces/${other.id}`)
  }
})

test('sidebar: failed deletion keeps confirmation open and retry succeeds', async ({
  page,
  request,
}) => {
  const workspace = await (
    await request.post('/api/workspaces', { data: { name: 'Проверка ошибки удаления' } })
  ).json()
  try {
    await page.goto('/')
    const picker = page.getByLabel('Рабочее пространство', { exact: true })
    await expect(picker).toBeEnabled()
    await picker.selectOption(workspace.id)
    await page.route(`**/api/workspaces/${workspace.id}`, async (route) => {
      if (route.request().method() === 'DELETE')
        await route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({ detail: 'Сначала остановите активные запуски.' }),
        })
      else await route.continue()
    })
    await page.getByTitle('Удалить рабочее пространство', { exact: true }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByRole('button', { name: 'Удалить пространство', exact: true }).click()
    await expect(dialog.getByRole('alert')).toHaveText('Сначала остановите активные запуски.')
    await expect(dialog.getByRole('button', { name: 'Отмена', exact: true })).toBeEnabled()
    await page.unroute(`**/api/workspaces/${workspace.id}`)
    await dialog.getByRole('button', { name: 'Удалить пространство', exact: true }).click()
    await expect(dialog).not.toBeVisible()
    await expect(picker.locator('option', { hasText: workspace.name })).toHaveCount(0)
  } finally {
    await request.delete(`/api/workspaces/${workspace.id}`)
  }
})
