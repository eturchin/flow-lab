import {
  Activity,
  ArrowUpRight,
  FlaskConical,
  Layers3,
  Plus,
  Radio,
  Workflow,
  Pencil,
  Trash2,
  ChevronDown,
  PanelsTopLeft,
} from 'lucide-react'
import type { Scenario, Workspace } from '../../types'

export function Sidebar({
  scenarios,
  selectedId,
  disabled,
  onSelect,
  onCreate,
  onAbout,
  workspaces,
  workspaceId,
  onWorkspaceSelect,
  onWorkspaceCreate,
  onWorkspaceRename,
  onWorkspaceDelete,
  onDeleteScenario,
}: {
  scenarios: Scenario[]
  selectedId?: string
  disabled: boolean
  onSelect: (scenario: Scenario) => void
  onCreate: () => void
  onAbout: () => void
  workspaces: Workspace[]
  workspaceId: string | null
  onWorkspaceSelect: (id: string) => void
  onWorkspaceCreate: () => void
  onWorkspaceRename: () => void
  onWorkspaceDelete: () => void
  onDeleteScenario: (scenario: Scenario) => void
}) {
  return (
    <aside className="sidebar">
      <a className="brand" href="/" aria-label="FlowLab — главная">
        <div className="brand-mark">
          <Workflow size={24} />
        </div>
        <span>
          FlowLab<small>BACKEND SIMULATION LAB</small>
        </span>
      </a>
      <div className="workspace-label">РАБОЧЕЕ ПРОСТРАНСТВО</div>
      <div className="workspace-controls">
        <div className="workspace-picker">
          <span className="workspace-icon" aria-hidden="true">
            <PanelsTopLeft size={20} />
          </span>
          <div className="workspace-picker-content">
            <span className="workspace-picker-caption">Текущее пространство</span>
            <select
              aria-label="Рабочее пространство"
              disabled={disabled}
              value={workspaceId ?? ''}
              title={
                workspaces.find((workspace) => workspace.id === workspaceId)?.name ??
                'Нет пространств'
              }
              onChange={(event) => onWorkspaceSelect(event.target.value)}
            >
              {!workspaces.length && <option value="">Нет пространств</option>}
              {workspaces.map((workspace) => (
                <option key={workspace.id} value={workspace.id}>
                  {workspace.name}
                </option>
              ))}
            </select>
          </div>
          <ChevronDown className="workspace-chevron" size={15} aria-hidden="true" />
        </div>
        <div className="workspace-actions">
          <button
            disabled={disabled}
            title="Создать рабочее пространство"
            onClick={onWorkspaceCreate}
          >
            <Plus size={14} /> <span>Создать</span>
          </button>
          <button
            disabled={disabled || !workspaceId}
            title="Переименовать рабочее пространство"
            onClick={onWorkspaceRename}
          >
            <Pencil size={16} />
          </button>
          <button
            disabled={disabled || !workspaceId}
            title="Удалить рабочее пространство"
            onClick={onWorkspaceDelete}
          >
            <Trash2 size={16} />
          </button>
        </div>
      </div>
      <div className="nav-active">
        <FlaskConical size={18} /> Лаборатория <span>01</span>
      </div>
      <div className="scenario-label">
        <span>СЦЕНАРИИ</span>
        <span>{String(scenarios.length).padStart(2, '0')}</span>
      </div>
      <nav className="scenario-list" aria-label="Сценарии" tabIndex={0}>
        {!scenarios.length && (
          <p className="sidebar-empty">
            Пока нет сценариев.
            <br />
            Создайте первый эксперимент.
          </p>
        )}
        {scenarios.map((scenario, index) => (
          <div className="scenario-row" key={scenario.id}>
            <button
              disabled={disabled}
              className={`scenario-link ${scenario.id === selectedId ? 'selected' : ''}`}
              key={scenario.id}
              onClick={() => onSelect(scenario)}
            >
              <Layers3 size={17} />
              <span>
                {scenario.name}
                <small>
                  {scenario.settings.nodes.filter((n) => n.kind !== 'Queue').length} сервиса · seed{' '}
                  {scenario.settings.seed}
                </small>
              </span>
              <em>{String(index + 1).padStart(2, '0')}</em>
            </button>
            <button
              className="scenario-delete"
              disabled={disabled}
              title={`Удалить сценарий «${scenario.name}»`}
              onClick={() => onDeleteScenario(scenario)}
            >
              <Trash2 size={14} />
            </button>
          </div>
        ))}
      </nav>
      <button className="new-scenario" disabled={disabled || !workspaceId} onClick={onCreate}>
        <Plus size={16} /> Новый сценарий
      </button>
      <div className="sidebar-bottom">
        <div className="lab-note">
          <Radio size={19} />
          <strong>
            Сначала смоделируйте.
            <br />
            Потом масштабируйте.
          </strong>
          <p>
            Понимайте поведение системы
            <br />
            до первой реальной нагрузки.
          </p>
          <button onClick={onAbout}>
            О математической модели <ArrowUpRight size={15} />
          </button>
        </div>
        <div className="version">
          <Activity size={14} />
          <span>FlowLab / v1.0</span>
          <i>.NET 10</i>
        </div>
      </div>
    </aside>
  )
}
