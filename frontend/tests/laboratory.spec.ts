import { test, expect } from '@playwright/test'

test('UI: create, configure, run, compare and delete an experiment', async ({ page }) => {
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(error.message))
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Архитектура под нагрузкой.' })).toBeVisible()
  await page.getByRole('button', { name: 'Новый сценарий', exact: true }).click()
  const name = `UI experiment ${Date.now()}`
  await page.getByLabel('Название сценария').fill(name)
  await page.getByRole('spinbutton', { name: /^Длительность/ }).fill('3')
  await page.getByRole('button', { name: 'Запустить', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Диагностика узлов' })).toBeVisible()
  await page.getByRole('spinbutton', { name: /^Интенсивность/ }).fill('150')
  await page.getByRole('button', { name: 'Запустить', exact: true }).click()
  await expect(page.locator('.history-panel tbody tr')).toHaveCount(2)
  await expect(page.locator('.history-panel input[type=checkbox]').first()).toBeEnabled()
  await page.locator('.history-panel input[type=checkbox]').nth(0).check()
  await page.locator('.history-panel input[type=checkbox]').nth(1).check()
  await page.getByRole('button', { name: /Сравнить/ }).click()
  await expect(page.getByRole('heading', { name: 'Сравнение экспериментов' })).toBeVisible()
  await page.getByRole('button', { name: /Разобрать ошибки/ }).click()
  await expect(page.getByRole('columnheader', { name: 'Причина', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Скрыть журнал ошибок' }).click()
  await page.getByRole('button', { name: 'Очередь', exact: true }).click()
  await page.getByRole('button', { name: 'Загрузка', exact: true }).click()
  await page.evaluate(() => window.scrollTo(0, 0))
  await page.screenshot({ path: '../artifacts/flowlab-desktop.png', fullPage: true })
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.getByRole('heading', { name: 'Архитектура под нагрузкой.' })).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
  ).toBeTruthy()
  await page.screenshot({ path: '../artifacts/flowlab-mobile.png', fullPage: true })
  await page.setViewportSize({ width: 1440, height: 1000 })
  await page.getByTitle('Удалить сценарий', { exact: true }).click()
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Удалить сценарий', exact: true })
    .click()
  await expect(page.getByRole('heading', { name, exact: true })).toHaveCount(0)
  expect(errors).toEqual([])
})
