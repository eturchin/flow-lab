import { statusLabels } from './lib/runStatus'
import { lazy, Suspense, useState } from 'react'
import {
  ArrowRight,
  Check,
  ChevronRight,
  CircleHelp,
  FlaskConical,
  LoaderCircle,
  Play,
  RotateCcw,
  ShieldCheck,
  Square,
  Workflow,
  X,
} from 'lucide-react'
import { useLab } from './hooks/useLab'
import { Sidebar } from './components/layout/Sidebar'
import { ModelDialog } from './components/layout/ModelDialog'
import { ConfigurationPanel } from './components/editor/ConfigurationPanel'
import { TopologyCanvas } from './components/topology/TopologyCanvas'
import { MetricsPanel } from './components/results/MetricsPanel'
const ChartsPanel = lazy(() =>
  import('./components/results/ChartsPanel').then((module) => ({ default: module.ChartsPanel })),
)
import { NodeResults } from './components/results/NodeResults'
import { RunHistory } from './components/runs/RunHistory'
import { defaultNode } from './types'
import type { NodeKind, Settings } from './types'
import './App.css'
import { Guide } from './components/Guide'
import { ActionDialog } from './components/layout/ActionDialog'
import type { DialogAction } from './components/layout/ActionDialog'
import type { Scenario } from './types'
import { toGraph } from './lib/graph'

function App() {
  const lab = useLab()
  const [selectedNode, setSelectedNode] = useState('source')
  const [editorTab, setEditorTab] = useState<'parameters' | 'resilience'>('parameters')
  const [about, setAbout] = useState(false)
  const [page, setPage] = useState<'lab' | 'guide'>('lab')
  const draft = lab.draft
  const workspace = lab.workspaces.find((w) => w.id === lab.workspaceId)
  const [action, setAction] = useState<DialogAction | null>(null)
  function deleteScenario(scenario: Scenario) {
    setAction({
      title: 'Удалить сценарий?',
      description: `Сценарий «${scenario.name}» и вся история его запусков будут удалены без возможности восстановления.`,
      confirmLabel: 'Удалить сценарий',
      onConfirm: () => lab.remove(scenario),
    })
  }
  const updateSettings = (settings: Settings) =>
    lab.setDraft((previous) => (previous ? { ...previous, settings } : previous))
  function addNode(kind: NodeKind) {
    if (!draft) return
    const next = defaultNode(kind)
    const settings = kind === 'Broker' ? toGraph(draft.settings) : draft.settings
    updateSettings({
      ...settings,
      nodes: [...settings.nodes, next],
      ...(settings.connections != null
        ? { connections: [...settings.connections, { from: 'source', to: next.id }] }
        : {}),
    })
    setSelectedNode(next.id)
    setEditorTab('parameters')
  }

  return (
    <div className="app-shell">
      <Sidebar
        workspaces={lab.workspaces}
        workspaceId={lab.workspaceId}
        onWorkspaceSelect={(id) => {
          void lab.selectWorkspace(id)
          setSelectedNode('source')
        }}
        onWorkspaceCreate={() =>
          setAction({
            title: 'Новое рабочее пространство',
            description: 'Сценарии нового пространства будут храниться отдельно.',
            confirmLabel: 'Создать',
            initialName: '',
            onConfirm: (name) => lab.saveWorkspace(name),
          })
        }
        onWorkspaceRename={() =>
          workspace &&
          setAction({
            title: 'Переименовать пространство',
            description: 'Введите новое название рабочего пространства.',
            confirmLabel: 'Сохранить',
            initialName: workspace.name,
            onConfirm: (name) => lab.saveWorkspace(name, workspace.id),
          })
        }
        onWorkspaceDelete={() =>
          workspace &&
          setAction({
            title: 'Удалить рабочее пространство?',
            description: `Пространство «${workspace.name}», все его сценарии (${lab.scenarios.length}) и история запусков будут удалены без возможности восстановления.`,
            confirmLabel: 'Удалить пространство',
            onConfirm: () => lab.removeWorkspace(workspace.id),
          })
        }
        onDeleteScenario={deleteScenario}
        scenarios={lab.scenarios}
        selectedId={draft?.id}
        disabled={lab.busy || lab.active}
        onSelect={(scenario) => {
          void lab.select(scenario)
          setSelectedNode('source')
        }}
        onCreate={() => {
          void lab.create()
          setSelectedNode('source')
        }}
        onAbout={() => setAbout(true)}
      />
      <main className="main-shell">
        <header className="topbar">
          <div className="breadcrumbs">
            <FlaskConical size={15} />
            <span>{workspace?.name ?? 'Рабочие пространства'}</span>
            <ChevronRight size={13} />
            <strong>Лаборатория</strong>
          </div>
          <div className="topbar-right">
            <span className="environment">
              <i /> Локальная среда
            </span>
            <button className="icon-button" title="О модели" onClick={() => setAbout(true)}>
              <CircleHelp size={18} />
            </button>
            <span className="avatar">FL</span>
          </div>
        </header>
        <div className="page-content">
          <div className="page-heading">
            <div>
              <div className="eyebrow">
                <span /> ENGINEERING PLAYGROUND
              </div>
              <h1>
                Архитектура под нагрузкой<span>.</span>
              </h1>
              <p>Моделируйте. Находите узкие места. Проверяйте решения.</p>
            </div>
            <div className="engine-badge">
              <Workflow size={18} />
              <span>
                Discrete-event engine<small>Воспроизводимо по seed</small>
              </span>
            </div>
          </div>
          <div className="editor-tabs" role="tablist" aria-label="Разделы лаборатории">
            <button
              role="tab"
              aria-selected={page === 'lab'}
              className={page === 'lab' ? 'active' : ''}
              onClick={() => setPage('lab')}
            >
              Лаборатория
            </button>
            <button
              role="tab"
              aria-selected={page === 'guide'}
              className={page === 'guide' ? 'active' : ''}
              onClick={() => setPage('guide')}
            >
              Инструкция и примеры
            </button>
          </div>
          {page === 'guide' ? (
            <Guide />
          ) : (
            <>
              {lab.error && (
                <div role="alert" className="alert error">
                  <span>{lab.error}</span>
                  <div>
                    <button onClick={() => void lab.reload()}>Повторить</button>
                    <button onClick={lab.clearError} aria-label="Закрыть ошибку">
                      <X size={16} />
                    </button>
                  </div>
                </div>
              )}
              {lab.notice && (
                <div role="status" className="alert success">
                  <Check size={15} />
                  {lab.notice}
                </div>
              )}
              {lab.loading ? (
                <div className="loading-state">
                  <LoaderCircle className="spin" size={26} />
                  <h2>Подключаем лабораторию…</h2>
                </div>
              ) : !draft ? (
                <div className="empty-state">
                  <FlaskConical size={42} />
                  <h2>Каждая система начинается со сценария</h2>
                  <p>
                    {workspace
                      ? 'Создайте первый эксперимент и задайте поток запросов.'
                      : 'Создайте рабочее пространство для ваших сценариев.'}
                  </p>
                  <button
                    className="button primary"
                    disabled={lab.busy}
                    onClick={() =>
                      workspace
                        ? void lab.create()
                        : setAction({
                            title: 'Новое рабочее пространство',
                            description: 'Введите название.',
                            confirmLabel: 'Создать',
                            initialName: '',
                            onConfirm: (name) => lab.saveWorkspace(name),
                          })
                    }
                  >
                    {workspace ? 'Создать сценарий' : 'Создать пространство'}{' '}
                    <ArrowRight size={15} />
                  </button>
                </div>
              ) : (
                <>
                  <section className="experiment-bar">
                    <div className="experiment-title">
                      <span className="experiment-icon">
                        <FlaskConical size={20} />
                      </span>
                      <div>
                        <span className="eyebrow">
                          ТЕКУЩИЙ ЭКСПЕРИМЕНТ {lab.dirty && <em>· НЕ СОХРАНЁН</em>}
                        </span>
                        <h2>{draft.name}</h2>
                      </div>
                    </div>
                    <div className="experiment-controls">
                      <span
                        className={`status-badge ${lab.run?.status.toLowerCase() ?? 'created'}`}
                      >
                        <i />
                        {lab.run ? statusLabels[lab.run.status] : 'Создан'}
                      </span>
                      <button
                        className="button"
                        disabled={lab.busy || lab.active}
                        onClick={lab.reset}
                        title="Сбросить несохранённые изменения"
                      >
                        <RotateCcw size={15} />
                        <span className="hide-small">Сбросить</span>
                      </button>
                      {lab.active ? (
                        <button
                          className="button stop"
                          disabled={lab.busy}
                          onClick={() => void lab.cancel()}
                        >
                          <Square size={14} /> Остановить
                        </button>
                      ) : (
                        <button
                          className="button primary"
                          disabled={lab.busy}
                          onClick={() => void lab.start()}
                        >
                          {lab.busy ? (
                            <LoaderCircle size={15} className="spin" />
                          ) : (
                            <Play size={15} fill="currentColor" />
                          )}{' '}
                          Запустить
                        </button>
                      )}
                    </div>
                  </section>
                  {lab.active && (
                    <div
                      className="run-progress"
                      role="progressbar"
                      aria-label="Прогресс симуляции"
                      aria-valuenow={lab.run?.progress ?? 0}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    >
                      <div>
                        <span>
                          {lab.run?.status === 'Queued'
                            ? 'Ожидание свободного обработчика'
                            : 'Обработка виртуальных событий'}{' '}
                          · {lab.run?.progress.toFixed(0)}%
                        </span>
                        <span>{lab.connected ? 'SignalR подключён' : 'HTTP обновления'}</span>
                      </div>
                      {lab.liveMetrics && (
                        <div className="live-metrics">
                          <span>Поступило: {lab.liveMetrics.total.toLocaleString('ru-RU')}</span>
                          <span>Успешно: {lab.liveMetrics.succeeded.toLocaleString('ru-RU')}</span>
                          <span>Ошибок: {lab.liveMetrics.failed.toLocaleString('ru-RU')}</span>
                          <span>Очередь: {lab.liveMetrics.point.queueLength}</span>
                        </div>
                      )}
                      <i style={{ width: `${Math.max(1, lab.run?.progress ?? 0)}%` }} />
                    </div>
                  )}
                  <div className="laboratory-grid">
                    <div className="visualization-column">
                      <TopologyCanvas
                        onChange={updateSettings}
                        settings={draft.settings}
                        selectedNode={selectedNode}
                        result={lab.run?.result ?? null}
                        active={lab.active}
                        onSelect={(id) => {
                          setSelectedNode(id)
                          setEditorTab('parameters')
                        }}
                        onAdd={addNode}
                      />
                      <div className="results-heading">
                        <h2>Результаты симуляции</h2>
                        <span>
                          {lab.run?.result
                            ? `ЗАПУСК ${lab.run.id.slice(0, 8)} · SEED ${lab.run.snapshot.settings.seed}`
                            : 'ВАШ СЛЕДУЮЩИЙ ИНСАЙТ — В ОДНОМ ЗАПУСКЕ'}
                        </span>
                      </div>
                      <MetricsPanel result={lab.run?.result ?? null} />
                      <Suspense fallback={<div className="chart-panel chart-container" />}>
                        <ChartsPanel points={lab.points} active={lab.active} />
                      </Suspense>
                    </div>
                    <ConfigurationPanel
                      scenario={draft}
                      disabled={lab.busy || lab.active}
                      dirty={lab.dirty}
                      selectedNode={selectedNode}
                      onSelectNode={setSelectedNode}
                      tab={editorTab}
                      onTabChange={setEditorTab}
                      onChange={lab.setDraft}
                      onSave={() => void lab.save()}
                      onDelete={() => deleteScenario(draft)}
                    />
                  </div>
                  {lab.run?.result && <NodeResults result={lab.run.result} runId={lab.run.id} />}
                  <RunHistory
                    runs={lab.runs}
                    currentId={lab.run?.id}
                    onView={lab.viewRun}
                    onCompare={(a, b) => void lab.compare(a, b)}
                    comparison={lab.comparison}
                    busy={lab.busy || lab.active}
                  />
                  <footer className="page-footer">
                    <span>
                      <ShieldCheck size={14} /> Учебная модель. Результаты зависят от допущений.
                    </span>
                    <button onClick={() => setAbout(true)}>
                      Как это работает <ArrowRight size={13} />
                    </button>
                  </footer>
                </>
              )}
            </>
          )}
        </div>
      </main>
      <ModelDialog open={about} onClose={() => setAbout(false)} />
      {action && <ActionDialog action={action} error={lab.error} onClose={() => setAction(null)} />}
    </div>
  )
}

export default App
