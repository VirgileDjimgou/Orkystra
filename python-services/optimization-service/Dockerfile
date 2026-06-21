FROM python:3.12-slim
WORKDIR /app
RUN pip install --no-cache-dir fastapi uvicorn[standard] ortools
COPY python-services/optimization-service/src /app/src
ENV PYTHONPATH=/app/src
EXPOSE 8002
CMD ["uvicorn", "orkystra_optimization_service.app:app", "--host", "0.0.0.0", "--port", "8002"]
