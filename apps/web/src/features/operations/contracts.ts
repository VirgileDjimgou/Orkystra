export type OperationsQueueSummaryResponse = {
  totalActive: number;
  criticalCount: number;
  warningCount: number;
  snoozedCount: number;
  unassignedCount: number;
};

export type OperationsExceptionLinkResponse = {
  missionId: string | null;
  missionReference: string | null;
  vehicleId: string | null;
  vehicleRegistrationNumber: string | null;
  driverId: string | null;
  driverName: string | null;
  alertId: string | null;
  inspectionId: string | null;
  syncIncidentId: string | null;
};

export type OperationsExceptionListItemResponse = {
  id: string;
  sourceType: "Alert" | "MissionDelay" | "CriticalDefect" | "DriverSync";
  severity: "Critical" | "Warning" | "Info";
  workflowStatus: "Open" | "Acknowledged" | "Snoozed" | "Resolved";
  title: string;
  message: string;
  detectedAtUtc: string;
  snoozedUntilUtc: string | null;
  snoozeReason: string | null;
  resolvedAtUtc: string | null;
  resolutionReason: string | null;
  assignedToUserId: string | null;
  assignedToDisplayName: string | null;
  acknowledgedByUserId: string | null;
  acknowledgedByDisplayName: string | null;
  searchText: string;
  sourceRowVersion: number;
  stateRowVersion: number;
  concurrencyToken: string;
  links: OperationsExceptionLinkResponse;
};

export type OperationsExceptionQueueResponse = {
  summary: OperationsQueueSummaryResponse;
  items: OperationsExceptionListItemResponse[];
};

export type OperationsSavedViewFilterRequest = {
  search: string | null;
  sourceType: string | null;
  severity: string | null;
  workflowStatus: string | null;
  assignedToUserId: string | null;
  includeSnoozed: boolean;
};

export type OperationsSavedViewResponse = {
  id: string;
  name: string;
  isShared: boolean;
  filters: OperationsSavedViewFilterRequest;
  rowVersion: number;
  createdByUserId: string;
};

export type OperationsAssignRequest = {
  assignedToUserId: string;
  concurrencyToken: string;
};

export type OperationsResolveRequest = {
  concurrencyToken: string;
  reason: string;
};

export type OperationsSnoozeRequest = {
  concurrencyToken: string;
  snoozedUntilUtc: string;
  reason: string;
};

export type OperationsActionRequest = {
  concurrencyToken: string;
};

export type OperationsBulkActionRequest = {
  action: string;
  reason: string | null;
  snoozedUntilUtc: string | null;
  assignedToUserId: string | null;
  items: Array<{ id: string; concurrencyToken: string }>;
};
