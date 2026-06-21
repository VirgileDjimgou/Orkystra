from __future__ import annotations

from .models import RagIngestionPlan


def build_rag_ingestion_plan() -> RagIngestionPlan:
    return RagIngestionPlan(
        collections=[
            "tenant_operational_docs",
            "warehouse_sops",
            "transport_playbooks",
            "incident_postmortems",
        ],
        document_types=[
            "SOP markdown",
            "warehouse manuals",
            "dispatcher playbooks",
            "incident reviews",
        ],
        ingestion_stages=[
            "normalize source documents into canonical markdown",
            "chunk by operational procedure or decision unit",
            "embed with tenant and context metadata",
            "store chunk provenance and version tags",
        ],
        retrieval_policy=[
            "prefer tenant-scoped documents",
            "require provenance on every retrieved chunk",
            "fall back to projections when knowledge retrieval is empty",
            "surface missing evidence explicitly instead of guessing",
        ],
    )
