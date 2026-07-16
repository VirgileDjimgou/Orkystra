const alertRuleTypes = [
  "VehicleDocumentExpiry",
  "DriverDocumentExpiry",
  "VehicleMaintenanceByDate",
  "VehicleMaintenanceByMileage",
  "VehicleInactive",
] as const;

const alertSeverities = ["Info", "Warning", "Critical"] as const;
const alertStatuses = ["Open", "Acknowledged", "Resolved"] as const;
const alertNotificationChannels = ["InApp", "EmailDev"] as const;

export type AlertRuleType = (typeof alertRuleTypes)[number];

export type AlertSeverity = (typeof alertSeverities)[number];
export type AlertStatus = (typeof alertStatuses)[number];
export type AlertNotificationChannel =
  (typeof alertNotificationChannels)[number];

export type AlertListItemResponse = {
  id: string;
  ruleType: AlertRuleType;
  severity: AlertSeverity;
  status: AlertStatus;
  title: string;
  message: string;
  targetType: string;
  targetEntityId: string;
  targetLabel: string;
  assignedToUserId: string | null;
  assignedToDisplayName: string | null;
  acknowledgedByUserId: string | null;
  acknowledgedByDisplayName: string | null;
  lastDetectedAtUtc: string;
  assignedAtUtc: string | null;
  acknowledgedAtUtc: string | null;
  resolvedAtUtc: string | null;
  rowVersion: number;
};

export type AlertNotificationResponse = {
  id: string;
  alertId: string;
  channel: AlertNotificationChannel;
  subject: string;
  body: string;
  sentAtUtc: string;
};

export type AlertSummaryResponse = {
  openCount: number;
  acknowledgedCount: number;
  criticalCount: number;
  warningCount: number;
  inactiveVehicleCount: number;
  maintenanceCount: number;
  complianceCount: number;
  topAlerts: AlertListItemResponse[];
  recentNotifications: AlertNotificationResponse[];
};

export type AlertAssigneeResponse = {
  userId: string;
  fullName: string;
  email: string;
  role: string;
};

export type ScanAlertsResponse = {
  createdAlerts: number;
  refreshedAlerts: number;
  resolvedAlerts: number;
  inAppNotifications: number;
  emailNotifications: number;
  emailFailures: number;
};

export type CreateComplianceDocumentRequest = {
  documentType: string;
  documentNumber: string;
  expiresAtUtc: string;
  notes: string | null;
};

export type CreateVehicleMaintenancePlanRequest = {
  title: string;
  intervalKilometers: number | null;
  intervalDays: number | null;
  lastCompletedOdometerKm: number;
  lastCompletedAtUtc: string;
};

type AlertListItemApiResponse = Omit<
  AlertListItemResponse,
  "ruleType" | "severity" | "status"
> & {
  ruleType: AlertRuleType | number;
  severity: AlertSeverity | number;
  status: AlertStatus | number;
};

type AlertNotificationApiResponse = Omit<
  AlertNotificationResponse,
  "channel"
> & {
  channel: AlertNotificationChannel | number;
};

type AlertSummaryApiResponse = Omit<
  AlertSummaryResponse,
  "topAlerts" | "recentNotifications"
> & {
  topAlerts: AlertListItemApiResponse[];
  recentNotifications: AlertNotificationApiResponse[];
};

function normalizeIndexedEnum<T extends readonly string[]>(
  values: T,
  value: T[number] | number,
): T[number] {
  if (typeof value === "number") {
    return values[value - 1] ?? values[0];
  }

  return value;
}

export function normalizeAlertRuleType(
  value: AlertRuleType | number,
): AlertRuleType {
  return normalizeIndexedEnum(alertRuleTypes, value);
}

export function normalizeAlertSeverity(
  value: AlertSeverity | number,
): AlertSeverity {
  return normalizeIndexedEnum(alertSeverities, value);
}

export function normalizeAlertStatus(value: AlertStatus | number): AlertStatus {
  return normalizeIndexedEnum(alertStatuses, value);
}

export function normalizeAlertNotificationChannel(
  value: AlertNotificationChannel | number,
): AlertNotificationChannel {
  return normalizeIndexedEnum(alertNotificationChannels, value);
}

export function normalizeAlertListItemResponse(
  response: AlertListItemApiResponse,
): AlertListItemResponse {
  return {
    ...response,
    ruleType: normalizeAlertRuleType(response.ruleType),
    severity: normalizeAlertSeverity(response.severity),
    status: normalizeAlertStatus(response.status),
  };
}

export function normalizeAlertNotificationResponse(
  response: AlertNotificationApiResponse,
): AlertNotificationResponse {
  return {
    ...response,
    channel: normalizeAlertNotificationChannel(response.channel),
  };
}

export function normalizeAlertSummaryResponse(
  response: AlertSummaryApiResponse,
): AlertSummaryResponse {
  return {
    ...response,
    topAlerts: response.topAlerts.map(normalizeAlertListItemResponse),
    recentNotifications: response.recentNotifications.map(
      normalizeAlertNotificationResponse,
    ),
  };
}
