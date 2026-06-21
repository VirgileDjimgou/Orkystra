from __future__ import annotations

import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
AI_SRC = ROOT / "ai-service" / "src"

if str(AI_SRC) not in sys.path:
    sys.path.insert(0, str(AI_SRC))

from orkystra_ai_service.agents import classify_intent, run_dispatcher_agent, run_warehouse_agent
from orkystra_ai_service.graph import build_supervisor
from orkystra_ai_service.models import ConfidenceLevel, IntentType, RecommendationRequest
from orkystra_ai_service.projections import build_demo_projection_snapshot
from orkystra_ai_service.rag import build_rag_ingestion_plan


class AiServiceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.snapshot = build_demo_projection_snapshot()

    def test_classify_intent_routes_warehouse_question(self) -> None:
        self.assertEqual(classify_intent("Which dock is under pressure?"), IntentType.WAREHOUSE)

    def test_classify_intent_routes_dispatcher_question(self) -> None:
        self.assertEqual(classify_intent("Which route should the dispatcher recover first?"), IntentType.DISPATCHER)

    def test_warehouse_agent_returns_grounded_answer(self) -> None:
        response = run_warehouse_agent(self.snapshot)

        self.assertEqual(response.intent, IntentType.WAREHOUSE)
        self.assertGreaterEqual(len(response.evidence), 2)
        self.assertGreaterEqual(len(response.recommended_actions), 1)
        self.assertIn("North Hub A", response.direct_answer)

    def test_dispatcher_agent_returns_route_recommendation(self) -> None:
        response = run_dispatcher_agent(self.snapshot)

        self.assertEqual(response.intent, IntentType.DISPATCHER)
        self.assertEqual(response.confidence_level, ConfidenceLevel.HIGH)
        self.assertIn("RT-412", response.direct_answer)

    def test_supervisor_fallback_marks_missing_intent(self) -> None:
        supervisor, graph_description = build_supervisor()
        request = RecommendationRequest(
            tenant_id="north-hub-demo",
            question="Can you help me?",
            scenario_id=None,
            projections=self.snapshot,
        )

        response = supervisor(request)

        self.assertIn(graph_description.mode, {"langgraph", "fallback"})
        self.assertEqual(response.intent, IntentType.UNKNOWN)
        self.assertIn("clear operational intent", response.missing_data)

    def test_rag_plan_declares_grounding_policy(self) -> None:
        plan = build_rag_ingestion_plan()

        self.assertIn("tenant_operational_docs", plan.collections)
        self.assertIn("surface missing evidence explicitly instead of guessing", plan.retrieval_policy)


if __name__ == "__main__":
    unittest.main()
