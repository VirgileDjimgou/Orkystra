from __future__ import annotations

from fastapi import FastAPI

from .graph import build_graph_description, build_supervisor
from .models import GraphDescription, RecommendationRequest, RecommendationResponse
from .projections import build_demo_projection_snapshot
from .rag import build_rag_ingestion_plan


def create_app() -> FastAPI:
    supervisor, graph_description = build_supervisor()

    app = FastAPI(
        title="Orkystra AI Service",
        description="Grounded logistics assistance over projections, with explicit evidence and assumptions.",
        version="0.1.0",
    )

    @app.get("/health")
    def health() -> dict[str, str]:
        return {"service": "orkystra-ai-service", "status": "healthy"}

    @app.get("/graph", response_model=GraphDescription)
    def graph() -> GraphDescription:
        return graph_description

    @app.get("/projections/demo", response_model=dict)
    def demo_projections() -> dict:
        return build_demo_projection_snapshot().model_dump(mode="json")

    @app.get("/rag/plan", response_model=dict)
    def rag_plan() -> dict:
        return build_rag_ingestion_plan().model_dump(mode="json")

    @app.post("/recommendations", response_model=RecommendationResponse)
    def recommendations(request: RecommendationRequest) -> RecommendationResponse:
        return supervisor(request)

    @app.get("/recommendations/demo/warehouse", response_model=RecommendationResponse)
    def warehouse_demo() -> RecommendationResponse:
        request = RecommendationRequest(
            tenant_id="north-hub-demo",
            question="Which warehouse area needs attention right now?",
            scenario_id="9d4e8f09-cf15-48d8-90a6-e96c833fd741",
            projections=build_demo_projection_snapshot(),
        )
        return supervisor(request)

    @app.get("/recommendations/demo/dispatcher", response_model=RecommendationResponse)
    def dispatcher_demo() -> RecommendationResponse:
        request = RecommendationRequest(
            tenant_id="north-hub-demo",
            question="Which route should a dispatcher review first?",
            scenario_id="9d4e8f09-cf15-48d8-90a6-e96c833fd741",
            projections=build_demo_projection_snapshot(),
        )
        return supervisor(request)

    return app


app = create_app()
