from __future__ import annotations

from collections.abc import Callable
from typing import Any

from .agents import classify_intent, run_dispatcher_agent, run_warehouse_agent
from .models import GraphDescription, IntentType, RecommendationRequest, RecommendationResponse

GraphRunner = Callable[[RecommendationRequest], RecommendationResponse]

GRAPH_NODES = [
    "intent_classifier",
    "context_loader",
    "warehouse_retriever",
    "transport_retriever",
    "validator_node",
    "response_formatter",
]

GRAPH_EDGES = [
    "intent_classifier -> context_loader",
    "context_loader -> warehouse_retriever",
    "context_loader -> transport_retriever",
    "warehouse_retriever -> validator_node",
    "transport_retriever -> validator_node",
    "validator_node -> response_formatter",
]


def build_graph_description(mode: str) -> GraphDescription:
    return GraphDescription(mode=mode, nodes=GRAPH_NODES, edges=GRAPH_EDGES)


def build_supervisor() -> tuple[GraphRunner, GraphDescription]:
    try:
        from langgraph.graph import END, START, StateGraph
    except Exception:
        return _run_supervisor, build_graph_description("fallback")

    state_graph: StateGraph[dict[str, Any]] = StateGraph(dict)
    state_graph.add_node("intent_classifier", _intent_classifier_node)
    state_graph.add_node("context_loader", _context_loader_node)
    state_graph.add_node("warehouse_retriever", _warehouse_retriever_node)
    state_graph.add_node("transport_retriever", _transport_retriever_node)
    state_graph.add_node("validator_node", _validator_node)
    state_graph.add_node("response_formatter", _response_formatter_node)

    state_graph.add_edge(START, "intent_classifier")
    state_graph.add_edge("intent_classifier", "context_loader")
    state_graph.add_conditional_edges(
        "context_loader",
        _route_by_intent,
        {
            "warehouse_retriever": "warehouse_retriever",
            "transport_retriever": "transport_retriever",
            "validator_node": "validator_node",
        },
    )
    state_graph.add_edge("warehouse_retriever", "validator_node")
    state_graph.add_edge("transport_retriever", "validator_node")
    state_graph.add_edge("validator_node", "response_formatter")
    state_graph.add_edge("response_formatter", END)

    compiled_graph = state_graph.compile()

    def run_graph(request: RecommendationRequest) -> RecommendationResponse:
        result = compiled_graph.invoke({"request": request})
        return result["response"]

    return run_graph, build_graph_description("langgraph")


def _run_supervisor(request: RecommendationRequest) -> RecommendationResponse:
    intent = classify_intent(request.question)
    if intent is IntentType.WAREHOUSE:
        return run_warehouse_agent(request.projections)
    if intent is IntentType.DISPATCHER:
        return run_dispatcher_agent(request.projections)

    return RecommendationResponse(
        intent=IntentType.UNKNOWN,
        direct_answer=(
            "I could not classify the request confidently from the current projections. "
            "Please ask a warehouse or dispatcher question, or provide more operational context."
        ),
        evidence=[],
        assumptions=["Intent routing stayed conservative because the request did not clearly match the warehouse or dispatcher tools."],
        recommended_actions=[],
        confidence_level="low",
        alternative_scenario_note=None,
        missing_data=["clear operational intent"],
        specialist_agents=["supervisor-agent"],
    )


def _intent_classifier_node(state: dict[str, Any]) -> dict[str, Any]:
    request: RecommendationRequest = state["request"]
    state["intent"] = classify_intent(request.question)
    return state


def _context_loader_node(state: dict[str, Any]) -> dict[str, Any]:
    state["projections"] = state["request"].projections
    return state


def _route_by_intent(state: dict[str, Any]) -> str:
    intent = state["intent"]
    if intent is IntentType.WAREHOUSE:
        return "warehouse_retriever"
    if intent is IntentType.DISPATCHER:
        return "transport_retriever"
    return "validator_node"


def _warehouse_retriever_node(state: dict[str, Any]) -> dict[str, Any]:
    request: RecommendationRequest = state["request"]
    state["response"] = run_warehouse_agent(request.projections)
    return state


def _transport_retriever_node(state: dict[str, Any]) -> dict[str, Any]:
    request: RecommendationRequest = state["request"]
    state["response"] = run_dispatcher_agent(request.projections)
    return state


def _validator_node(state: dict[str, Any]) -> dict[str, Any]:
    if "response" not in state:
        request: RecommendationRequest = state["request"]
        state["response"] = _run_supervisor(request)
    return state


def _response_formatter_node(state: dict[str, Any]) -> dict[str, Any]:
    return state
