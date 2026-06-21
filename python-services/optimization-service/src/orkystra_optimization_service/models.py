from __future__ import annotations

from enum import Enum

from pydantic import BaseModel, Field


class OptimizationStatus(str, Enum):
    OPTIMIZED = "optimized"
    INFEASIBLE = "infeasible"


class TimeWindow(BaseModel):
    start_minute: int = Field(ge=0)
    end_minute: int = Field(ge=0)


class Depot(BaseModel):
    depot_id: str
    name: str


class Vehicle(BaseModel):
    vehicle_id: str
    reference: str
    capacity: int = Field(gt=0)
    shift_end_minute: int = Field(gt=0)
    cost_per_km: float = Field(gt=0)


class Stop(BaseModel):
    stop_id: str
    reference: str
    demand: int = Field(gt=0)
    service_minutes: int = Field(ge=0)
    priority: int = Field(ge=1, le=10)
    time_window: TimeWindow


class ConstraintSet(BaseModel):
    max_route_minutes: int = Field(gt=0)
    allow_late_service: bool = False


class RouteOptimizationRequest(BaseModel):
    tenant_id: str
    scenario_id: str | None = None
    depot: Depot
    vehicle: Vehicle
    stops: list[Stop] = Field(min_length=1)
    travel_time_matrix: list[list[int]]
    distance_matrix: list[list[int]]
    constraints: ConstraintSet


class RouteLeg(BaseModel):
    from_reference: str
    to_reference: str
    travel_minutes: int
    arrival_minute: int
    service_start_minute: int
    departure_minute: int


class SolutionExplanation(BaseModel):
    selected_vehicle_reason: str
    prioritization_reason: str
    tight_constraints: list[str] = Field(default_factory=list)
    infeasibility_reason: str | None = None
    trade_offs: list[str] = Field(default_factory=list)


class AlternativeSolution(BaseModel):
    label: str
    ordered_stop_references: list[str]
    objective_score: float
    summary: str


class RouteOptimizationResponse(BaseModel):
    status: OptimizationStatus
    objective_score: float | None = None
    ordered_stop_references: list[str] = Field(default_factory=list)
    eta_minutes: dict[str, int] = Field(default_factory=dict)
    load_distribution: dict[str, int] = Field(default_factory=dict)
    constraint_violations: list[str] = Field(default_factory=list)
    explanation: SolutionExplanation
    alternatives: list[AlternativeSolution] = Field(default_factory=list)
    solver_backend: str
