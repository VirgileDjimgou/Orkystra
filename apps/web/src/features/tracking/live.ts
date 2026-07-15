import * as signalR from "@microsoft/signalr";
import type { TrackingPositionResponse } from "./contracts";

export type TrackingConnectionState =
  "idle" | "connecting" | "live" | "reconnecting" | "offline";

export async function connectTrackingStream(
  token: string,
  onPosition: (position: TrackingPositionResponse) => void,
  onStateChange: (state: TrackingConnectionState) => void,
): Promise<signalR.HubConnection> {
  onStateChange("connecting");

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/tracking", {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .build();

  connection.on("trackingPositionChanged", onPosition);
  connection.onreconnecting(() => {
    onStateChange("reconnecting");
  });
  connection.onreconnected(() => {
    onStateChange("live");
  });
  connection.onclose(() => {
    onStateChange("offline");
  });

  await connection.start();
  onStateChange("live");
  return connection;
}
