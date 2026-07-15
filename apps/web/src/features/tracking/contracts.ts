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
