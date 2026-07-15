export type VehicleResponse = {
  id: string;
  registrationNumber: string;
  displayName: string;
  isActive: boolean;
  rowVersion: number;
};

export type CreateVehicleRequest = {
  registrationNumber: string;
  displayName: string;
};

export type UpdateVehicleRequest = {
  displayName: string;
  rowVersion: number;
};

export type DriverResponse = {
  id: string;
  fullName: string;
  licenseNumber: string;
  phoneNumber: string | null;
  isActive: boolean;
  rowVersion: number;
};

export type CreateDriverRequest = {
  fullName: string;
  licenseNumber: string;
  phoneNumber: string | null;
};

export type UpdateDriverRequest = {
  fullName: string;
  phoneNumber: string | null;
  rowVersion: number;
};

export type ActiveAssignmentResponse = {
  assignmentId: string;
  vehicleId: string;
  vehicleRegistrationNumber: string;
  assignedAtUtc: string;
};

export type GpsDeviceResponse = {
  id: string;
  serialNumber: string;
  displayName: string | null;
  isActive: boolean;
  rowVersion: number;
  activeAssignment: ActiveAssignmentResponse | null;
};

export type CreateGpsDeviceRequest = {
  serialNumber: string;
  displayName: string | null;
};

export type UpdateGpsDeviceRequest = {
  displayName: string | null;
  rowVersion: number;
};

export type AssignDeviceRequest = {
  vehicleId: string;
};

export type AssignmentResponse = {
  id: string;
  deviceId: string;
  vehicleId: string;
  assignedAtUtc: string;
  unassignedAtUtc: string | null;
  isActive: boolean;
};

export type ImportSummary = {
  created: number;
  updated: number;
  skipped: number;
  errors: string[];
};
