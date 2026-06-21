from pathlib import Path


def test_python_service_skeletons_exist() -> None:
    root = Path(__file__).resolve().parents[1]

    assert (root / "ai-service" / "src" / "orkystra_ai_service" / "app.py").exists()
    assert (
        root
        / "optimization-service"
        / "src"
        / "orkystra_optimization_service"
        / "app.py"
    ).exists()
