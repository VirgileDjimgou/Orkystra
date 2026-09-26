export type CockpitSelection = {
  vehicleId: string | null;
  missionId: string | null;
  exceptionId: string | null;
};

export type CockpitDockTab = "exceptions" | "missions" | "timeline";
