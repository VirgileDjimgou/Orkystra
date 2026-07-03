from __future__ import annotations

import sys
from pathlib import Path

from fastapi.testclient import TestClient


ROOT = Path(__file__).resolve().parents[1]
OPT_SRC = ROOT / "optimization-service" / "src"

if str(OPT_SRC) not in sys.path:
    sys.path.insert(0, str(OPT_SRC))

from orkystra_optimization_service.app import create_app


def test_health_endpoint_reports_service_status() -> None:
    client = TestClient(create_app())

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "service": "orkystra-optimization-service",
        "status": "healthy",
    }


def test_capabilities_endpoint_reports_supported_solver_features() -> None:
    client = TestClient(create_app())

    response = client.get("/capabilities")

    assert response.status_code == 200
    payload = response.json()
    assert payload["solver_backend"] in {"ortools", "deterministic-fallback"}
    assert "single-vehicle route sequencing" in payload["supports"]


def test_demo_optimization_endpoint_returns_solution_payload() -> None:
    client = TestClient(create_app())

    response = client.get("/optimize/demo")

    assert response.status_code == 200
    payload = response.json()
    assert payload["status"] in {"optimized", "infeasible"}
    assert payload["solver_backend"] in {"ortools", "deterministic-fallback"}
