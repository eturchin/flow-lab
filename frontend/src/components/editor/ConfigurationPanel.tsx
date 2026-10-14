import { Save, ShieldCheck, Trash2 } from 'lucide-react'
import type { Scenario, Settings } from '../../types'
import { SourceEditor } from './SourceEditor'
import { NodeEditor } from './NodeEditor'
import { ResilienceEditor } from './ResilienceEditor'
import { SelectField } from '../ui/Field'

type EditorTab = 'parameters' | 'resilience'
interface ConfigurationPanelProps {
  scenario: Scenario
  disabled: boolean
  dirty: boolean
  selectedNode: string
  onSelectNode: (id: string) => void
  tab: EditorTab
  onTabChange: (tab: EditorTab) => void
  onChange: (scenario: Scenario) => void
  onSave: () => void
  onDelete: () => void
}
export function ConfigurationPanel({
  scenario: draft,
  disabled,
  dirty,
  selectedNode,
  onSelectNode: setSelectedNode,
  tab: editorTab,
  onTabChange: setEditorTab,
  onChange,
  onSave,
  onDelete,
}: ConfigurationPanelProps) {
  const nodeIndex = draft.settings.nodes.findIndex((node) => node.id === selectedNode)
  const node = draft.settings.nodes[nodeIndex]
  const updateSettings = (settings: Settings) => onChange({ ...draft, settings })
  function moveNode(direction: number) {
    const nodes = [...draft.settings.nodes]
    const target = nodeIndex + direction
    if (nodeIndex < 0 || target < 0 || target >= nodes.length) return
    const current = nodes[nodeIndex]
    nodes[nodeIndex] = nodes[target]
    nodes[target] = current
    updateSettings({ ...draft.settings, nodes })
  }
  return (
    <aside className="settings-panel">
      <div className="settings-heading">
        <h2>Конфигурация</h2>
        <button
          className="icon-button"
          title="Сохранить настройки"
          disabled={disabled}
          onClick={onSave}
        >
          <Save size={17} />
        </button>
      </div>
      <div className="editor-tabs">
        <button
          className={editorTab === 'parameters' ? 'active' : ''}
          onClick={() => setEditorTab('parameters')}
        >
          Параметры
        </button>
        <button
          className={editorTab === 'resilience' ? 'active' : ''}
          onClick={() => setEditorTab('resilience')}
        >
          <ShieldCheck size={14} /> Устойчивость
        </button>
      </div>
      <fieldset disabled={disabled} className="editor-fieldset">
        {editorTab === 'resilience' ? (
          <ResilienceEditor
            value={draft.settings.resilience}
            onChange={(resilience) => updateSettings({ ...draft.settings, resilience })}
          />
        ) : (
          <>
            <div className="scenario-editor">
              <label className="field">
                <span>Название сценария</span>
                <input
                  value={draft.name}
                  maxLength={120}
                  onChange={(event) =>
                    onChange({
                      ...draft,
                      name: event.target.value,
                    })
                  }
                />
              </label>
              <label className="field">
                <span>Описание</span>
                <textarea
                  rows={2}
                  maxLength={2000}
                  value={draft.description}
                  onChange={(event) =>
                    onChange({
                      ...draft,
                      description: event.target.value,
                    })
                  }
                />
              </label>
            </div>
            {node ? (
              <NodeEditor
                graph={draft.settings.connections != null}
                node={node}
                index={nodeIndex}
                total={draft.settings.nodes.length}
                onChange={(next) =>
                  updateSettings({
                    ...draft.settings,
                    nodes: draft.settings.nodes.map((n) => (n.id === next.id ? next : n)),
                  })
                }
                onDelete={() => {
                  updateSettings({
                    ...draft.settings,
                    nodes: draft.settings.nodes.filter((n) => n.id !== node.id),
                    ...(draft.settings.connections != null
                      ? {
                          connections: draft.settings.connections.filter(
                            (e) => e.from !== node.id && e.to !== node.id,
                          ),
                        }
                      : {}),
                  })
                  setSelectedNode('source')
                }}
                onMove={moveNode}
              />
            ) : (
              <>
                <SourceEditor settings={draft.settings} onChange={updateSettings} />
                {draft.settings.connections != null && (
                  <section className="editor-section">
                    <SelectField
                      label="Маршрутизация источника"
                      value={draft.settings.sourceRouting ?? 'Broadcast'}
                      onChange={(value) =>
                        updateSettings({
                          ...draft.settings,
                          sourceRouting: value as 'Broadcast' | 'RoundRobin',
                        })
                      }
                    >
                      <option value="Broadcast">Во все API параллельно</option>
                      <option value="RoundRobin">В один API по очереди</option>
                    </SelectField>
                  </section>
                )}
              </>
            )}
          </>
        )}
      </fieldset>
      <div className="settings-footer">
        <span>
          <i />
          {dirty ? 'Есть несохранённые изменения' : 'Все изменения сохранены'}
        </span>
        <button
          className="icon-button danger-icon"
          disabled={disabled}
          onClick={onDelete}
          title="Удалить сценарий"
        >
          <Trash2 size={14} />
        </button>
      </div>
    </aside>
  )
}
