# AI Layer 1

Sprint 7 introduces the first grounded AI service skeleton in `python-services/ai-service`.

## Design principles

- AI reads projection-shaped inputs and does not invent domain state.
- Every answer separates direct answer, evidence, assumptions, recommended actions, confidence, and missing data.
- When context is incomplete, the service degrades gracefully and says what is missing.
- LangGraph is optional at runtime in this sprint; the service can fall back to a deterministic supervisor path while keeping the graph shape explicit.

## Current endpoints

- `GET /health`
- `GET /graph`
- `GET /projections/demo`
- `GET /rag/plan`
- `POST /recommendations`
- `GET /recommendations/demo/warehouse`
- `GET /recommendations/demo/dispatcher`

## Current agents

- `supervisor-agent`: routes intent and enforces the response shape
- `warehouse-agent`: analyzes warehouse summary projections
- `dispatcher-agent`: analyzes route summary projections

## Response contract

The main response model contains:

- `intent`
- `direct_answer`
- `evidence`
- `assumptions`
- `recommended_actions`
- `confidence_level`
- `alternative_scenario_note`
- `missing_data`
- `specialist_agents`

## RAG plan boundary

Sprint 7 does not implement live retrieval yet. It only defines:

- target collections
- source document types
- ingestion stages
- retrieval grounding policy

This keeps the AI layer grounded in the current product architecture without pretending that Qdrant or live document ingestion already exists.
