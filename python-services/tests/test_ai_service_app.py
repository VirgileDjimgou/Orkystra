from __future__ import annotations

import sys
from pathlib import Path

from fastapi.testclient import TestClient


ROOT = Path(__file__).resolve().parents[1]
AI_SRC = ROOT / "ai-service" / "src"

if str(AI_SRC) not in sys.path:
    sys.path.insert(0, str(AI_SRC))

from orkystra_ai_service.app import create_app


def test_health_endpoint_reports_service_status() -> None:
    client = TestClient(create_app())

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "service": "orkystra-ai-service",
        "status": "healthy",
    }


def test_graph_endpoint_exposes_supervisor_mode() -> None:
    client = TestClient(create_app())

    response = client.get("/graph")

    assert response.status_code == 200
    payload = response.json()
    assert payload["mode"] in {"langgraph", "fallback"}
    assert len(payload["nodes"]) >= 1


def test_demo_recommendation_endpoint_returns_grounded_response() -> None:
    client = TestClient(create_app())

    response = client.get("/recommendations/demo/warehouse")

    assert response.status_code == 200
    payload = response.json()
    assert payload["intent"] == "warehouse"
    assert len(payload["evidence"]) >= 1
    assert len(payload["recommended_actions"]) >= 1
