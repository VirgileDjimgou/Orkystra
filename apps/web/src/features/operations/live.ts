import * as signalR from "@microsoft/signalr";

export type OperationsConnectionState =
  "idle" | "connecting" | "live" | "reconnecting" | "offline";

export async function connectOperationsStream(
  onQueueChanged: () => void,
  onStateChange: (state: OperationsConnectionState) => void,
): Promise<signalR.HubConnection> {
  onStateChange("connecting");

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/operations", { withCredentials: true })
    .withAutomaticReconnect()
    .build();

  connection.on("operationsQueueChanged", onQueueChanged);
  connection.onreconnecting(() => onStateChange("reconnecting"));
  connection.onreconnected(() => {
    onStateChange("live");
    onQueueChanged();
  });
  connection.onclose(() => onStateChange("offline"));

  await connection.start();
  onStateChange("live");
  return connection;
}
