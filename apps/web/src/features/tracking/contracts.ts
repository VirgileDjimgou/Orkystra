export type TrackingPositionResponse = {
  vehicleId: string;
  registrationNumber: string;
  displayName: string;
  deviceId: string;
  recordedAtUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  headingDegrees: number;
  sequenceNumber?: number | null;
  accuracyMeters?: number | null;
  source?: string;
  qualityScore?: number;
  qualityStatus?: "Fresh" | "Delayed" | "Inaccurate" | "Invalid" | "Silent";
  qualityReason?: string;
};

export type TrackingHistoryItemResponse = {
  eventId: string;
  vehicleId: string;
  deviceId: string;
  recordedAtUtc: string;
  ingestedAtUtc: string;
  latitude: number;
  longitude: number;
  speedKph: number;
  headingDegrees: number;
  sequenceNumber?: number | null;
  accuracyMeters?: number | null;
  source?: string;
  qualityScore?: number;
  anomalyFlags?: string;
};

export type TrackingHistoryPageResponse = {
  page: number;
  pageSize: number;
  totalCount: number;
  items: TrackingHistoryItemResponse[];
};

export type TrackingMetricsResponse = {
  currentVehicleCount: number;
  historyPointCount: number;
  acceptedCount: number;
  duplicateCount: number;
  outOfOrderCount: number;
  retentionDays: number;
};

export type TrackingDiagnosticResponse = {
  vehicleId: string;
  registrationNumber: string;
  displayName: string;
  driverName: string | null;
  deviceId: string;
  lastCommunicationAtUtc: string | null;
  status: "Fresh" | "Delayed" | "Inaccurate" | "Invalid" | "Silent";
  reason: string;
  qualityScore: number;
  accuracyMeters: number | null;
  source: string;
  sequenceNumber: number | null;
};
