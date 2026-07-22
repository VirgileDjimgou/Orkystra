import * as signalR from "@microsoft/signalr";
import type { TrackingPositionResponse } from "./contracts";

export type TrackingConnectionState =
  "idle" | "connecting" | "live" | "reconnecting" | "offline";

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

  let pending: TrackingPositionResponse | null = null;
  let timer: number | undefined;
  connection.on(
    "trackingPositionChanged",
    (position: TrackingPositionResponse) => {
      pending = position;
      if (timer !== undefined) return;
      timer = window.setTimeout(() => {
        if (pending) onPosition(pending);
        pending = null;
        timer = undefined;
      }, 250);
    },
  );
  connection.onreconnecting(() => {
    onStateChange("reconnecting");
  });
  connection.onreconnected(async () => {
    onStateChange("live");
    await onCatchUp?.();
  });
  connection.onclose(() => {
    onStateChange("offline");
  });

  await connection.start();
  onStateChange("live");
  return connection;
}
