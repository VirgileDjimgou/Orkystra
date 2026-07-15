import { defineStore } from "pinia";
import { apiRequest } from "../../services/api";
import type {
  AssignmentResponse,
  CreateDriverRequest,
  CreateGpsDeviceRequest,
  CreateVehicleRequest,
  DriverResponse,
  GpsDeviceResponse,
  ImportSummary,
  UpdateDriverRequest,
  UpdateGpsDeviceRequest,
  UpdateVehicleRequest,
  VehicleResponse,
} from "./contracts";

type AsyncStatus = "idle" | "loading" | "success" | "error";

export const useFleetStore = defineStore("fleet", {
  state: () => ({
    vehicles: [] as VehicleResponse[],
    drivers: [] as DriverResponse[],
    devices: [] as GpsDeviceResponse[],
    assignments: [] as AssignmentResponse[],
    vehiclesStatus: "idle" as AsyncStatus,
    driversStatus: "idle" as AsyncStatus,
    devicesStatus: "idle" as AsyncStatus,
    assignmentsStatus: "idle" as AsyncStatus,
    vehiclesError: "",
    driversError: "",
    devicesError: "",
    assignmentsError: "",
    actionError: "",
    lastImport: null as ImportSummary | null,
  }),
  actions: {
    async loadVehicles(token: string) {
      this.vehiclesStatus = "loading";
      this.vehiclesError = "";
      try {
        this.vehicles = await apiRequest<VehicleResponse[]>(
          "/api/v1/fleet/vehicles",
          { token },
        );
        this.vehiclesStatus = "success";
      } catch (error) {
        this.vehiclesStatus = "error";
        this.vehiclesError =
          error instanceof Error
            ? "Unable to load vehicles."
            : "Unable to load vehicles.";
      }
    },
    async createVehicle(
      token: string,
      request: CreateVehicleRequest,
    ): Promise<VehicleResponse | null> {
      this.actionError = "";
      try {
        const created = await apiRequest<VehicleResponse>(
          "/api/v1/fleet/vehicles",
          { method: "POST", token, body: request },
        );
        await this.loadVehicles(token);
        return created;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to create the vehicle."
            : "Unable to create the vehicle.";
        return null;
      }
    },
    async updateVehicle(
      token: string,
      id: string,
      request: UpdateVehicleRequest,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<VehicleResponse>(`/api/v1/fleet/vehicles/${id}`, {
          method: "PUT",
          token,
          body: request,
        });
        await this.loadVehicles(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Vehicle update failed. Reload the list and try again."
            : "Vehicle update failed.";
        return false;
      }
    },
    async setVehicleStatus(
      token: string,
      id: string,
      activate: boolean,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<VehicleResponse>(
          `/api/v1/fleet/vehicles/${id}/${activate ? "activate" : "deactivate"}`,
          { method: "POST", token },
        );
        await this.loadVehicles(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to update vehicle status."
            : "Unable to update vehicle status.";
        return false;
      }
    },
    async importVehicles(
      token: string,
      csv: string,
    ): Promise<ImportSummary | null> {
      this.actionError = "";
      try {
        const summary = await apiRequest<ImportSummary>(
          "/api/v1/fleet/vehicles/import",
          { method: "POST", token, body: csv, contentType: "text/csv" },
        );
        await this.loadVehicles(token);
        this.lastImport = summary;
        return summary;
      } catch (error) {
        this.actionError =
          error instanceof Error ? "Import failed." : "Import failed.";
        return null;
      }
    },
    async loadDrivers(token: string) {
      this.driversStatus = "loading";
      this.driversError = "";
      try {
        this.drivers = await apiRequest<DriverResponse[]>(
          "/api/v1/fleet/drivers",
          { token },
        );
        this.driversStatus = "success";
      } catch {
        this.driversStatus = "error";
        this.driversError = "Unable to load drivers.";
      }
    },
    async createDriver(
      token: string,
      request: CreateDriverRequest,
    ): Promise<DriverResponse | null> {
      this.actionError = "";
      try {
        const created = await apiRequest<DriverResponse>(
          "/api/v1/fleet/drivers",
          { method: "POST", token, body: request },
        );
        await this.loadDrivers(token);
        return created;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to create the driver."
            : "Unable to create the driver.";
        return null;
      }
    },
    async updateDriver(
      token: string,
      id: string,
      request: UpdateDriverRequest,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<DriverResponse>(`/api/v1/fleet/drivers/${id}`, {
          method: "PUT",
          token,
          body: request,
        });
        await this.loadDrivers(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Driver update failed."
            : "Driver update failed.";
        return false;
      }
    },
    async setDriverStatus(
      token: string,
      id: string,
      activate: boolean,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<DriverResponse>(
          `/api/v1/fleet/drivers/${id}/${activate ? "activate" : "deactivate"}`,
          { method: "POST", token },
        );
        await this.loadDrivers(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to update driver status."
            : "Unable to update driver status.";
        return false;
      }
    },
    async importDrivers(
      token: string,
      csv: string,
    ): Promise<ImportSummary | null> {
      this.actionError = "";
      try {
        const summary = await apiRequest<ImportSummary>(
          "/api/v1/fleet/drivers/import",
          { method: "POST", token, body: csv, contentType: "text/csv" },
        );
        await this.loadDrivers(token);
        this.lastImport = summary;
        return summary;
      } catch (error) {
        this.actionError =
          error instanceof Error ? "Import failed." : "Import failed.";
        return null;
      }
    },
    async loadDevices(token: string) {
      this.devicesStatus = "loading";
      this.devicesError = "";
      try {
        this.devices = await apiRequest<GpsDeviceResponse[]>(
          "/api/v1/fleet/devices",
          { token },
        );
        this.devicesStatus = "success";
      } catch {
        this.devicesStatus = "error";
        this.devicesError = "Unable to load devices.";
      }
    },
    async importDevices(
      token: string,
      csv: string,
    ): Promise<ImportSummary | null> {
      this.actionError = "";
      try {
        const summary = await apiRequest<ImportSummary>(
          "/api/v1/fleet/devices/import",
          { method: "POST", token, body: csv, contentType: "text/csv" },
        );
        await this.loadDevices(token);
        this.lastImport = summary;
        return summary;
      } catch (error) {
        this.actionError =
          error instanceof Error ? "Import failed." : "Import failed.";
        return null;
      }
    },
    async createDevice(
      token: string,
      request: CreateGpsDeviceRequest,
    ): Promise<GpsDeviceResponse | null> {
      this.actionError = "";
      try {
        const created = await apiRequest<GpsDeviceResponse>(
          "/api/v1/fleet/devices",
          { method: "POST", token, body: request },
        );
        await this.loadDevices(token);
        return created;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to create the device."
            : "Unable to create the device.";
        return null;
      }
    },
    async updateDevice(
      token: string,
      id: string,
      request: UpdateGpsDeviceRequest,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<GpsDeviceResponse>(`/api/v1/fleet/devices/${id}`, {
          method: "PUT",
          token,
          body: request,
        });
        await this.loadDevices(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Device update failed."
            : "Device update failed.";
        return false;
      }
    },
    async setDeviceStatus(
      token: string,
      id: string,
      activate: boolean,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<GpsDeviceResponse>(
          `/api/v1/fleet/devices/${id}/${activate ? "activate" : "deactivate"}`,
          { method: "POST", token },
        );
        await this.loadDevices(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to update device status."
            : "Unable to update device status.";
        return false;
      }
    },
    async loadAssignments(token: string, deviceId: string) {
      this.assignmentsStatus = "loading";
      this.assignmentsError = "";
      try {
        this.assignments = await apiRequest<AssignmentResponse[]>(
          `/api/v1/fleet/devices/${deviceId}/assignments`,
          { token },
        );
        this.assignmentsStatus = "success";
      } catch {
        this.assignmentsStatus = "error";
        this.assignmentsError = "Unable to load device assignments.";
      }
    },
    async assignDevice(
      token: string,
      deviceId: string,
      vehicleId: string,
    ): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<AssignmentResponse>(
          `/api/v1/fleet/devices/${deviceId}/assignments/active`,
          { method: "POST", token, body: { vehicleId } },
        );
        await this.loadDevices(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to assign the device."
            : "Unable to assign the device.";
        return false;
      }
    },
    async closeAssignment(token: string, deviceId: string): Promise<boolean> {
      this.actionError = "";
      try {
        await apiRequest<AssignmentResponse>(
          `/api/v1/fleet/devices/${deviceId}/assignments/active/close`,
          { method: "POST", token },
        );
        await this.loadDevices(token);
        return true;
      } catch (error) {
        this.actionError =
          error instanceof Error
            ? "Unable to close the assignment."
            : "Unable to close the assignment.";
        return false;
      }
    },
  },
});
