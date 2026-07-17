export type ComplianceMatrixRow = {
  subjectType: "vehicle" | "driver";
  subjectId: string;
  subjectLabel: string;
  documentType: string;
  documentId: string | null;
  expiresAtUtc: string | null;
  status: string;
  isBlocking: boolean;
  isRisk: boolean;
};
export type CompliancePolicy = {
  blocksAssignments: boolean;
  rowVersion: number;
  disclaimer: string;
};
export type ComplianceDocumentType = {
  id: string;
  name: string;
  subjectType: "vehicle" | "driver";
  isBlocking: boolean;
  requiresReview: boolean;
  isActive: boolean;
  rowVersion: number;
};
export type Campaign = {
  id: string;
  name: string;
  opensAtUtc: string;
  closesAtUtc: string;
  status: string;
  rowVersion: number;
  tasks: Array<{
    id: string;
    vehicleRegistration: string;
    driverName: string;
    status: string;
    submittedAtUtc: string | null;
  }>;
};
