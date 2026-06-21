from __future__ import annotations

from .models import (
    ConfidenceLevel,
    EvidenceItem,
    IntentType,
    ProjectionSnapshot,
    RecommendedAction,
    RecommendationResponse,
)


def classify_intent(question: str) -> IntentType:
    normalized = question.lower()

    warehouse_keywords = ("warehouse", "dock", "slot", "pallet", "storage", "congestion")
    dispatcher_keywords = ("route", "truck", "delay", "eta", "dispatch", "carrier", "delivery")

    if any(keyword in normalized for keyword in warehouse_keywords):
        return IntentType.WAREHOUSE

    if any(keyword in normalized for keyword in dispatcher_keywords):
        return IntentType.DISPATCHER

    return IntentType.UNKNOWN


def run_warehouse_agent(projections: ProjectionSnapshot) -> RecommendationResponse:
    if not projections.warehouse_summaries:
        return RecommendationResponse(
            intent=IntentType.WAREHOUSE,
            direct_answer="I cannot assess warehouse conditions because no warehouse projections were provided.",
            evidence=[],
            assumptions=[],
            recommended_actions=[],
            confidence_level=ConfidenceLevel.LOW,
            missing_data=["warehouse summary projections"],
            specialist_agents=["warehouse-agent"],
        )

    busiest = max(
        projections.warehouse_summaries,
        key=lambda summary: summary.stored_pallet_count / max(summary.slot_count, 1),
    )
    utilization = round((busiest.stored_pallet_count / max(busiest.slot_count, 1)) * 100)

    evidence = [
        EvidenceItem(
            source="warehouse_summary_projection",
            detail=f"{busiest.name} is using {busiest.stored_pallet_count} of {busiest.slot_count} slots ({utilization}%).",
        ),
        EvidenceItem(
            source="dock_projection",
            detail=f"{busiest.name} currently shows {busiest.occupied_dock_count} occupied docks.",
        ),
    ]

    actions = [
        RecommendedAction(
            title=f"Rebalance inbound flow at {busiest.name}",
            rationale="The busiest warehouse has the tightest remaining slot capacity and should be protected from additional congestion.",
            priority="high" if utilization >= 80 else "medium",
        )
    ]

    assumptions: list[str] = []
    if busiest.occupied_dock_count == 0:
        assumptions.append("Dock pressure cannot be estimated reliably because the busiest warehouse reports no occupied docks.")

    return RecommendationResponse(
        intent=IntentType.WAREHOUSE,
        direct_answer=(
            f"The clearest warehouse pressure point is {busiest.name}. "
            f"It is operating at roughly {utilization}% slot utilization, so further inbound waves should be staged carefully."
        ),
        evidence=evidence,
        assumptions=assumptions,
        recommended_actions=actions,
        confidence_level=ConfidenceLevel.HIGH if utilization >= 70 else ConfidenceLevel.MEDIUM,
        alternative_scenario_note="Run a what-if scenario that diverts the next inbound wave to the lower-utilization warehouse before changing execution rules.",
        missing_data=[],
        specialist_agents=["warehouse-agent"],
    )


def run_dispatcher_agent(projections: ProjectionSnapshot) -> RecommendationResponse:
    if not projections.route_summaries:
        return RecommendationResponse(
            intent=IntentType.DISPATCHER,
            direct_answer="I cannot assess transport conditions because no route projections were provided.",
            evidence=[],
            assumptions=[],
            recommended_actions=[],
            confidence_level=ConfidenceLevel.LOW,
            missing_data=["route summary projections"],
            specialist_agents=["dispatcher-agent"],
        )

    delayed_routes = [route for route in projections.route_summaries if route.status.lower() != "on time"]
    critical_route = max(delayed_routes or projections.route_summaries, key=lambda route: route.shipment_count)

    evidence = [
        EvidenceItem(
            source="route_summary_projection",
            detail=(
                f"Route {critical_route.reference} is marked '{critical_route.status}' "
                f"with {critical_route.shipment_count} shipments over {critical_route.stop_count} stops."
            ),
        )
    ]

    actions = [
        RecommendedAction(
            title=f"Review recovery plan for {critical_route.reference}",
            rationale="This route currently carries the largest shipment load among the routes that are not fully on time.",
            priority="critical" if critical_route.status.lower() == "delayed" else "high",
        )
    ]

    missing_data: list[str] = []
    assumptions: list[str] = []
    if critical_route.completed_delivery_count == 0:
        assumptions.append("No completed deliveries are visible yet, so route recovery confidence is limited.")
    if not delayed_routes:
        assumptions.append("No delayed routes were present; the answer is based on the heaviest active route instead of an active exception.")
        missing_data.append("delay-specific telemetry")

    return RecommendationResponse(
        intent=IntentType.DISPATCHER,
        direct_answer=(
            f"The highest-impact transport watch item is {critical_route.reference}. "
            f"It is currently '{critical_route.status}' and affects {critical_route.shipment_count} shipments."
        ),
        evidence=evidence,
        assumptions=assumptions,
        recommended_actions=actions,
        confidence_level=ConfidenceLevel.HIGH if delayed_routes else ConfidenceLevel.MEDIUM,
        alternative_scenario_note="Compare the current route against a scenario with one stop resequenced or one carrier handoff reassigned.",
        missing_data=missing_data,
        specialist_agents=["dispatcher-agent"],
    )
