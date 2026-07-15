import { defineStore } from "pinia";
import { apiRequest } from "../../services/api";
import type {
  TrackingHistoryPageResponse,
  TrackingMetricsResponse,
  TrackingPositionResponse,
} from "./contracts";
import type { TrackingConnectionState } from "./live";

type AsyncStatus = "idle" | "loading" | "success" | "error";

export const useTrackingStore = defineStore("tracking", {
  state: () => ({
    positions: [] as TrackingPositionResponse[],
    metrics: null as TrackingMetricsResponse | null,
    history: null as TrackingHistoryPageResponse | null,
    positionsStatus: "idle" as AsyncStatus,
    historyStatus: "idle" as AsyncStatus,
    metricsStatus: "idle" as AsyncStatus,
    positionsError: "",
    historyError: "",
    metricsError: "",
    connectionState: "idle" as TrackingConnectionState,
  }),
  actions: {
    async loadPositions(token: string) {
      this.positionsStatus = "loading";
      this.positionsError = "";
      try {
        this.positions = await apiRequest<TrackingPositionResponse[]>(
          "/api/v1/tracking/positions",
          { token },
        );
        this.sortPositions();
        this.positionsStatus = "success";
      } catch {
        this.positionsStatus = "error";
        this.positionsError = "Unable to load current vehicle positions.";
      }
    },
    async loadMetrics(token: string) {
      this.metricsStatus = "loading";
      this.metricsError = "";
      try {
        this.metrics = await apiRequest<TrackingMetricsResponse>(
          "/api/v1/tracking/metrics",
          { token },
        );
        this.metricsStatus = "success";
      } catch {
        this.metricsStatus = "error";
        this.metricsError = "Unable to load tracking metrics.";
      }
    },
    async loadHistory(
      token: string,
      vehicleId: string,
      page = 1,
      pageSize = 5,
    ) {
      this.historyStatus = "loading";
      this.historyError = "";
      try {
        this.history = await apiRequest<TrackingHistoryPageResponse>(
          `/api/v1/tracking/history?vehicleId=${encodeURIComponent(vehicleId)}&page=${page}&pageSize=${pageSize}`,
          { token },
        );
        this.historyStatus = "success";
      } catch {
        this.historyStatus = "error";
        this.historyError = "Unable to load tracking history.";
      }
    },
    async refresh(token: string) {
      await Promise.all([this.loadPositions(token), this.loadMetrics(token)]);
    },
    applyLivePosition(position: TrackingPositionResponse) {
      const existing = this.positions.find(
        (item) => item.vehicleId === position.vehicleId,
      );
      if (existing) {
        existing.registrationNumber =
          position.registrationNumber || existing.registrationNumber;
        existing.displayName = position.displayName || existing.displayName;
        existing.deviceId = position.deviceId;
        existing.recordedAtUtc = position.recordedAtUtc;
        existing.latitude = position.latitude;
        existing.longitude = position.longitude;
        existing.speedKph = position.speedKph;
        existing.headingDegrees = position.headingDegrees;
      } else {
        this.positions.push(position);
      }

      if (this.metrics) {
        this.metrics.currentVehicleCount = this.positions.length;
      }

      this.sortPositions();
    },
    setConnectionState(state: TrackingConnectionState) {
      this.connectionState = state;
    },
    sortPositions() {
      this.positions.sort((left, right) =>
        left.registrationNumber.localeCompare(right.registrationNumber),
      );
    },
  },
});
