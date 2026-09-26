import * as signalR from "@microsoft/signalr";
import type { TrackingPositionResponse } from "./contracts";

export type TrackingConnectionState =
  "idle" | "connecting" | "live" | "reconnecting" | "offline";

const POSITION_BATCH_DELAY_MS = 250;

export function createPositionBatcher(
  onPosition: (position: TrackingPositionResponse) => void,
) {
  const pendingByVehicle = new Map<string, TrackingPositionResponse>();
  let timer: number | undefined;

  function flush() {
    timer = undefined;
    const positions = [...pendingByVehicle.values()];
    pendingByVehicle.clear();
    positions.forEach(onPosition);
  }

  return {
    enqueue(position: TrackingPositionResponse) {
      pendingByVehicle.set(position.vehicleId, position);
      if (timer === undefined) {
        timer = window.setTimeout(flush, POSITION_BATCH_DELAY_MS);
      }
    },
    clear() {
      pendingByVehicle.clear();
      if (timer !== undefined) {
        window.clearTimeout(timer);
        timer = undefined;
      }
    },
  };
}

export async function completeTrackingReconnect(
  onStateChange: (state: TrackingConnectionState) => void,
  onCatchUp?: () => Promise<void>,
) {
  await onCatchUp?.();
  onStateChange("live");
}

export async function connectTrackingStream(
  _transportMarker: string,
  onPosition: (position: TrackingPositionResponse) => void,
  onStateChange: (state: TrackingConnectionState) => void,
  onCatchUp?: () => Promise<void>,
): Promise<signalR.HubConnection> {
  onStateChange("connecting");

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tracking", { withCredentials: true })
    .withAutomaticReconnect()
    .build();

  const batcher = createPositionBatcher(onPosition);
  connection.on(
    "trackingPositionChanged",
    (position: TrackingPositionResponse) => {
      batcher.enqueue(position);
    },
  );
  connection.onreconnecting(() => {
    batcher.clear();
    onStateChange("reconnecting");
  });
  connection.onreconnected(async () => {
    await completeTrackingReconnect(onStateChange, onCatchUp);
  });
  connection.onclose(() => {
    batcher.clear();
    onStateChange("offline");
  });

  await connection.start();
  onStateChange("live");
  return connection;
}
