const missionStatuses = [
  "Draft",
  "Planned",
  "Assigned",
  "EnRoute",
  "Arrived",
  "Delayed",
  "Completed",
  "Cancelled",
] as const;

const missionTimelineEventTypes = [
  "Created",
  "Updated",
  "AssignmentChanged",
  "StatusChanged",
  "DelaySimulated",
] as const;

const defectSeverities = ["None", "Minor", "Major", "Critical"] as const;
const inspectionOutcomes = ["Passed", "Failed"] as const;

export type MissionStatus = (typeof missionStatuses)[number];

export type MissionTimelineEventType =
  (typeof missionTimelineEventTypes)[number];

export type DefectSeverity = (typeof defectSeverities)[number];
export type InspectionOutcome = (typeof inspectionOutcomes)[number];

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
  targetStatus: number;
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

export type MissionInspectionItemResponse = {
  sequence: number;
  code: string;
  label: string;
  isPass: boolean;
  defectSeverity: DefectSeverity;
  notes: string | null;
  photoReadUrl: string | null;
};

export type MissionInspectionResponse = {
  inspectionId: string;
  outcome: InspectionOutcome;
  hasBlockingCriticalDefect: boolean;
  completedAtUtc: string;
  notes: string | null;
  items: MissionInspectionItemResponse[];
};

export type MissionProofPhotoResponse = {
  mediaAssetId: string;
  caption: string | null;
  photoReadUrl: string;
};

export type MissionStopProofResponse = {
  proofId: string;
  missionStopId: string;
  recipientName: string;
  signatureName: string;
  deliveredAtUtc: string;
  notes: string | null;
  photos: MissionProofPhotoResponse[];
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
  latestInspection: MissionInspectionResponse | null;
  deliveryProofs: MissionStopProofResponse[];
  stops: MissionStopResponse[];
  timeline: MissionTimelineEventResponse[];
};

type MissionSummaryApiResponse = Omit<MissionSummaryResponse, "status"> & {
  status: MissionStatus | number;
};

type MissionInspectionItemApiResponse = Omit<
  MissionInspectionItemResponse,
  "defectSeverity"
> & {
  defectSeverity: DefectSeverity | number;
};

type MissionInspectionApiResponse = Omit<
  MissionInspectionResponse,
  "outcome" | "items"
> & {
  outcome: InspectionOutcome | number;
  items: MissionInspectionItemApiResponse[];
};

type MissionTimelineEventApiResponse = Omit<
  MissionTimelineEventResponse,
  "eventType"
> & {
  eventType: MissionTimelineEventType | number;
};

type MissionDetailApiResponse = Omit<
  MissionDetailResponse,
  "status" | "latestInspection" | "timeline"
> & {
  status: MissionStatus | number;
  latestInspection: MissionInspectionApiResponse | null;
  timeline: MissionTimelineEventApiResponse[];
};

function normalizeIndexedEnum<T extends readonly string[]>(
  values: T,
  value: T[number] | number,
  offset = 0,
): T[number] {
  if (typeof value === "number") {
    return values[value - offset] ?? values[0];
  }

  return value;
}

export function serializeMissionStatus(status: MissionStatus): number {
  return missionStatuses.indexOf(status);
}

export function normalizeMissionStatus(
  value: MissionStatus | number,
): MissionStatus {
  return normalizeIndexedEnum(missionStatuses, value);
}

export function normalizeMissionTimelineEventType(
  value: MissionTimelineEventType | number,
): MissionTimelineEventType {
  return normalizeIndexedEnum(missionTimelineEventTypes, value);
}

export function normalizeDefectSeverity(
  value: DefectSeverity | number,
): DefectSeverity {
  return normalizeIndexedEnum(defectSeverities, value);
}

export function normalizeInspectionOutcome(
  value: InspectionOutcome | number,
): InspectionOutcome {
  return normalizeIndexedEnum(inspectionOutcomes, value, 1);
}

export function normalizeMissionSummaryResponse(
  response: MissionSummaryApiResponse,
): MissionSummaryResponse {
  return {
    ...response,
    status: normalizeMissionStatus(response.status),
  };
}

export function normalizeMissionDetailResponse(
  response: MissionDetailApiResponse,
): MissionDetailResponse {
  return {
    ...response,
    status: normalizeMissionStatus(response.status),
    latestInspection: response.latestInspection
      ? {
          ...response.latestInspection,
          outcome: normalizeInspectionOutcome(
            response.latestInspection.outcome,
          ),
          items: response.latestInspection.items.map((item) => ({
            ...item,
            defectSeverity: normalizeDefectSeverity(item.defectSeverity),
          })),
        }
      : null,
    timeline: response.timeline.map((item) => ({
      ...item,
      eventType: normalizeMissionTimelineEventType(item.eventType),
    })),
  };
}
