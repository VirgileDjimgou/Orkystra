from __future__ import annotations

from enum import Enum

from pydantic import BaseModel, Field


class ConfidenceLevel(str, Enum):
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


class IntentType(str, Enum):
    WAREHOUSE = "warehouse"
    DISPATCHER = "dispatcher"
    UNKNOWN = "unknown"


class EvidenceItem(BaseModel):
    source: str
    detail: str


class RecommendedAction(BaseModel):
    title: str
    rationale: str
    priority: str = Field(pattern="^(critical|high|medium|low)$")


class WarehouseSummaryProjection(BaseModel):
    warehouse_id: str
    name: str
    zone_count: int
    rack_count: int
    slot_count: int
    occupied_dock_count: int
    stored_pallet_count: int


class RouteSummaryProjection(BaseModel):
    route_id: str
    reference: str
    truck_id: str
    truck_reference: str
    status: str
    stop_count: int
    shipment_count: int
    completed_delivery_count: int


class ScenarioSummaryProjection(BaseModel):
    scenario_id: str
    name: str
    seed: int
    status: str
    current_time: str
    injected_event_count: int


class ProjectionSnapshot(BaseModel):
    warehouse_summaries: list[WarehouseSummaryProjection] = Field(default_factory=list)
    route_summaries: list[RouteSummaryProjection] = Field(default_factory=list)
    scenario_summaries: list[ScenarioSummaryProjection] = Field(default_factory=list)


class RecommendationRequest(BaseModel):
    tenant_id: str
    question: str
    scenario_id: str | None = None
    projections: ProjectionSnapshot


class RecommendationResponse(BaseModel):
    intent: IntentType
    direct_answer: str
    evidence: list[EvidenceItem] = Field(default_factory=list)
    assumptions: list[str] = Field(default_factory=list)
    recommended_actions: list[RecommendedAction] = Field(default_factory=list)
    confidence_level: ConfidenceLevel
    alternative_scenario_note: str | None = None
    missing_data: list[str] = Field(default_factory=list)
    specialist_agents: list[str] = Field(default_factory=list)


class RagIngestionPlan(BaseModel):
    collections: list[str]
    document_types: list[str]
    ingestion_stages: list[str]
    retrieval_policy: list[str]


class GraphDescription(BaseModel):
    mode: str
    nodes: list[str]
    edges: list[str]
