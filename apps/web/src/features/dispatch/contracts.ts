export type MissionStatus =
  | "Draft"
  | "Planned"
  | "Assigned"
  | "EnRoute"
  | "Arrived"
  | "Delayed"
  | "Completed"
  | "Cancelled";

export type MissionTimelineEventType =
  | "Created"
  | "Updated"
  | "AssignmentChanged"
  | "StatusChanged"
  | "DelaySimulated";

export type MissionStopRequest = {
  sequence: number;
  name: string;
  address: string;
  plannedArrivalUtc: string;
};

export type CreateMissionRequest = {
  reference: string;
  title: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  stops: MissionStopRequest[];
};

export type SetMissionAssignmentRequest = {
  driverId: string;
  vehicleId: string;
  rowVersion: number;
};

export type TransitionMissionStatusRequest = {
  targetStatus: MissionStatus;
  rowVersion: number;
};

export type SimulateMissionDelayRequest = {
  delayMinutes: number;
  rowVersion: number;
};

export type MissionStopResponse = {
  id: string;
  sequence: number;
  name: string;
  address: string;
  plannedArrivalUtc: string;
};

export type MissionTimelineEventResponse = {
  id: string;
  eventType: MissionTimelineEventType;
  description: string;
  occurredAtUtc: string;
};

export type MissionSummaryResponse = {
  id: string;
  reference: string;
  title: string;
  status: MissionStatus;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  driverId: string | null;
  driverName: string | null;
  vehicleId: string | null;
  vehicleRegistrationNumber: string | null;
  stopCount: number;
  simulatedDelayMinutes: number;
  rowVersion: number;
  currentLatitude: number | null;
  currentLongitude: number | null;
};

export type MissionDetailResponse = {
  id: string;
  reference: string;
  title: string;
  status: MissionStatus;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  driverId: string | null;
  driverName: string | null;
  vehicleId: string | null;
  vehicleRegistrationNumber: string | null;
  simulatedDelayMinutes: number;
  rowVersion: number;
  stops: MissionStopResponse[];
  timeline: MissionTimelineEventResponse[];
};
