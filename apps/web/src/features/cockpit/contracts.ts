export type CockpitSelection = {
  vehicleId: string | null;
  missionId: string | null;
  exceptionId: string | null;
};

export type CockpitDockTab = "exceptions" | "missions" | "agents" | "timeline";

export type AgentActivityResponse = {
  id: string;
  agentId: string;
  driverId: string;
  vehicleId: string;
  missionId: string;
  sequence: number;
  observedState: string;
  policy: string;
  action: string;
  resultCode: string;
  resultMessage: string;
  occurredAtUtc: string;
};
