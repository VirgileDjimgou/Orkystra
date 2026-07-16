import { defineStore } from "pinia";
import { apiRequest } from "../../services/api";
import type {
  CreateMissionRequest,
  MissionDetailResponse,
  MissionStatus,
  MissionSummaryResponse,
  SetMissionAssignmentRequest,
  SimulateMissionDelayRequest,
} from "./contracts";
import {
  normalizeMissionDetailResponse,
  normalizeMissionSummaryResponse,
  serializeMissionStatus,
} from "./contracts";

type AsyncStatus = "idle" | "loading" | "success" | "error";

export const useDispatchStore = defineStore("dispatch", {
  state: () => ({
    missions: [] as MissionSummaryResponse[],
    selectedMission: null as MissionDetailResponse | null,
    missionsStatus: "idle" as AsyncStatus,
    detailStatus: "idle" as AsyncStatus,
    missionsError: "",
    detailError: "",
    actionError: "",
  }),
  actions: {
    async loadMissions(token: string) {
      this.missionsStatus = "loading";
      this.missionsError = "";
      try {
        const missions = await apiRequest<MissionSummaryResponse[]>(
          "/api/v1/dispatch/missions",
          { token },
        );
        this.missions = missions.map(normalizeMissionSummaryResponse);
        this.missionsStatus = "success";
      } catch {
        this.missionsStatus = "error";
        this.missionsError = "Unable to load missions.";
      }
    },
    async loadMission(token: string, missionId: string) {
      this.detailStatus = "loading";
      this.detailError = "";
      try {
        const mission = await apiRequest<MissionDetailResponse>(
          `/api/v1/dispatch/missions/${missionId}`,
          { token },
        );
        this.selectedMission = normalizeMissionDetailResponse(mission);
        this.detailStatus = "success";
      } catch {
        this.detailStatus = "error";
        this.detailError = "Unable to load mission details.";
      }
    },
    async createMission(
      token: string,
      request: CreateMissionRequest,
    ): Promise<MissionDetailResponse | null> {
      this.actionError = "";
      try {
        const created = normalizeMissionDetailResponse(
          await apiRequest<MissionDetailResponse>("/api/v1/dispatch/missions", {
            method: "POST",
            token,
            body: request,
          }),
        );
        await this.loadMissions(token);
        this.selectedMission = created;
        return created;
      } catch {
        this.actionError = "Unable to create the mission.";
        return null;
      }
    },
    async setAssignment(
      token: string,
      missionId: string,
      request: SetMissionAssignmentRequest,
    ): Promise<MissionDetailResponse | null> {
      this.actionError = "";
      try {
        const updated = normalizeMissionDetailResponse(
          await apiRequest<MissionDetailResponse>(
            `/api/v1/dispatch/missions/${missionId}/assignment`,
            { method: "PUT", token, body: request },
          ),
        );
        await this.loadMissions(token);
        this.selectedMission = updated;
        return updated;
      } catch {
        this.actionError = "Unable to assign the mission.";
        return null;
      }
    },
    async transitionStatus(
      token: string,
      missionId: string,
      targetStatus: MissionStatus,
      rowVersion: number,
    ): Promise<MissionDetailResponse | null> {
      this.actionError = "";
      const request = {
        targetStatus: serializeMissionStatus(targetStatus),
        rowVersion,
      };
      try {
        const updated = normalizeMissionDetailResponse(
          await apiRequest<MissionDetailResponse>(
            `/api/v1/dispatch/missions/${missionId}/status`,
            { method: "POST", token, body: request },
          ),
        );
        await this.loadMissions(token);
        this.selectedMission = updated;
        return updated;
      } catch {
        this.actionError = `Unable to move the mission to ${targetStatus}.`;
        return null;
      }
    },
    async simulateDelay(
      token: string,
      missionId: string,
      request: SimulateMissionDelayRequest,
    ): Promise<MissionDetailResponse | null> {
      this.actionError = "";
      try {
        const updated = normalizeMissionDetailResponse(
          await apiRequest<MissionDetailResponse>(
            `/api/v1/dispatch/missions/${missionId}/delay-simulation`,
            { method: "POST", token, body: request },
          ),
        );
        await this.loadMissions(token);
        this.selectedMission = updated;
        return updated;
      } catch {
        this.actionError = "Unable to simulate delay.";
        return null;
      }
    },
  },
});
