export interface TaskHistoryActor {
  id?:   string | null;
  name?: string | null;
  type?: string | null;
}

export interface TaskHistoryEvent {
  auditId:       string;
  eventType:     string;
  action:        string;
  description:   string;
  occurredAtUtc: string;
  actor:         TaskHistoryActor | null;
  before?:       string | null;
  after?:        string | null;
  metadata?:     string | null;
}

export interface TaskHistoryResponse {
  items:      TaskHistoryEvent[];
  totalCount: number;
  page:       number;
  pageSize:   number;
}
