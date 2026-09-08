import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { hubUrl } from "./events";

async function readHubToken() {
  const response = await fetch("/api/session/hub-token", {
    credentials: "same-origin",
  });
  if (!response.ok) {
    throw new Error("HUB_TOKEN_UNAVAILABLE");
  }
  const payload = (await response.json()) as { accessToken?: string };
  if (!payload.accessToken) {
    throw new Error("HUB_TOKEN_UNAVAILABLE");
  }
  return payload.accessToken;
}

export function createHubConnection() {
  return new HubConnectionBuilder()
    .withUrl(hubUrl(), {
      accessTokenFactory: readHubToken,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();
}
