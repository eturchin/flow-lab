export type Distribution = 'Fixed' | 'Exponential'
export type NodeKind = 'Service' | 'Queue' | 'External' | 'Broker'
export type RoutingMode = 'Broadcast' | 'RoundRobin'
export interface Connection {
  from: string
  to: string
}
export interface NodeSettings {
  id: string
  name: string
  kind: NodeKind
  parallelWithPrevious?: boolean
  routing?: RoutingMode
  queueDiscipline?: 'Fifo' | 'Lifo'
  workers: number
  processingMs: number
  distribution: Distribution
  variability: number
  errorProbability: number
  queueCapacity: number
  outageStartMs: number
  outageDurationMs: number
  outageLatencyMultiplier: number
  outageFails: boolean
}
export interface Resilience {
  timeoutMs: number
  maxAttempts: number
  retryDelayMs: number
  maxRetryDelayMs: number
  circuitBreaker: boolean
  failureThreshold: number
  breakDurationMs: number
}
export interface Settings {
  arrivalRate: number
  durationMs: number
  seed: number
  arrivalDistribution: Distribution
  resilience: Resilience
  nodes: NodeSettings[]
  connections?: Connection[] | null
  sourceRouting?: RoutingMode
}
export interface Scenario {
  workspaceId: string
  id: string
  name: string
  description: string
  settings: Settings
  updatedAt: string
}
export interface Workspace {
  id: string
  name: string
  createdAt: string
}
export interface Point {
  timeMs: number
  latencyMs: number
  queueLength: number
  utilization: number
  completed: number
}
export interface Result {
  total: number
  succeeded: number
  failed: number
  errorPercent: number
  throughput: number
  p50: number
  p95: number
  p99: number
  meanLatency: number
  averageQueue: number
  maxQueue: number
  dropped: number
  retries: number
  elapsedMs: number
  timeline: Point[]
  nodes: {
    id: string
    name: string
    utilization: number
    averageQueue: number
    maxQueue: number
    maxBusy: number
    started?: number
    breakerOpened: number
    breakerHalfOpened: number
    breakerClosed: number
  }[]
  failureReasons: Record<string, number>
}
export type Status = 'Queued' | 'Running' | 'Completed' | 'Cancelled' | 'Failed'
export interface Run {
  id: string
  scenarioId: string
  status: Status
  progress: number
  createdAt: string
  finishedAt: string | null
  snapshot: Pick<Scenario, 'name' | 'description' | 'settings'>
  result: Result | null
  error: string | null
}
export interface Comparison {
  baselineId: string
  candidateId: string
  metrics: {
    name: string
    baseline: number
    candidate: number
    absolute: number
    percent: number | null
  }[]
}
export const defaultNode = (kind: NodeKind): NodeSettings => ({
  id: crypto.randomUUID(),
  name:
    kind === 'Broker'
      ? 'RabbitMQ'
      : kind === 'External'
        ? 'External API'
        : kind === 'Queue'
          ? 'Request buffer'
          : 'API Service',
  kind,
  workers: 4,
  processingMs: 30,
  distribution: 'Exponential',
  variability: 0,
  errorProbability: 0.01,
  queueCapacity: 200,
  outageStartMs: 20000,
  outageDurationMs: 0,
  outageLatencyMultiplier: 5,
  outageFails: false,
})
export const defaultSettings = (): Settings => ({
  arrivalRate: 80,
  durationMs: 60000,
  seed: 42,
  arrivalDistribution: 'Exponential',
  resilience: {
    timeoutMs: 3000,
    maxAttempts: 1,
    retryDelayMs: 100,
    maxRetryDelayMs: 2000,
    circuitBreaker: false,
    failureThreshold: 5,
    breakDurationMs: 2000,
  },
  nodes: [
    defaultNode('Service'),
    {
      ...defaultNode('External'),
      name: 'Payment API',
      workers: 8,
      processingMs: 65,
      errorProbability: 0.03,
    },
  ],
})
