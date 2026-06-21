FROM python:3.12-slim
WORKDIR /app
RUN pip install --no-cache-dir fastapi uvicorn[standard] langgraph
COPY python-services/ai-service/src /app/src
ENV PYTHONPATH=/app/src
EXPOSE 8001
CMD ["uvicorn", "orkystra_ai_service.app:app", "--host", "0.0.0.0", "--port", "8001"]
