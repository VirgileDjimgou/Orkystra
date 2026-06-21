from __future__ import annotations

from fastapi import FastAPI

from .demo_data import build_demo_route_request
from .models import RouteOptimizationRequest, RouteOptimizationResponse
from .solver import ORTOOLS_AVAILABLE, solve_route_optimization


def create_app() -> FastAPI:
    app = FastAPI(
        title="Orkystra Optimization Service",
        description="Explainable route optimization for dispatcher and scenario workflows.",
        version="0.1.0",
    )

    @app.get("/health")
    def health() -> dict[str, str]:
        return {"service": "orkystra-optimization-service", "status": "healthy"}

    @app.get("/capabilities")
    def capabilities() -> dict[str, object]:
        return {
            "solver_backend": "ortools" if ORTOOLS_AVAILABLE else "deterministic-fallback",
            "supports": [
                "single-vehicle route sequencing",
                "time windows",
                "capacity constraints",
                "explainable alternatives",
            ],
        }

    @app.get("/optimize/demo", response_model=RouteOptimizationResponse)
    def optimize_demo() -> RouteOptimizationResponse:
        return solve_route_optimization(build_demo_route_request())

    @app.post("/optimize", response_model=RouteOptimizationResponse)
    def optimize(request: RouteOptimizationRequest) -> RouteOptimizationResponse:
        return solve_route_optimization(request)

    return app


app = create_app()
