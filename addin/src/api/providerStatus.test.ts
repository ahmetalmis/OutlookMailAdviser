import { afterEach, expect, it, vi } from "vitest";
import { analyzeMail, askMailQuestion, draftMail, getApiStatus } from "./mailAnalysisApi";
import type { AnalyzeMailRequest, DraftMailRequest, MailMessage } from "../types";

afterEach(() => vi.unstubAllGlobals());

it("shows the safe authentication reason on a 503 readiness response", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
    state: "unhealthy", code: "openai_authentication_failed", detail: "PRIVATE_API_KEY",
    data: { provider: "OpenAI" },
  }), { status: 503, headers: { "content-type": "application/json" } })));
  const status = await getApiStatus();
  expect(status.state).toBe("unhealthy");
  expect(status.detail).toContain("API anahtarı/token kabul edilmedi");
  expect(status.detail).not.toContain("PRIVATE_API_KEY");
  expect(status.provider).toBe("OpenAI");
});

it.each([analyzeMail, draftMail, askMailQuestion])("localizes provider failures for every mail operation", async (operation) => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
    code: "openai_quota_exceeded", detail: "PRIVATE_API_KEY",
  }), { status: 502, headers: { "content-type": "application/json" } })));
  const request = {} as AnalyzeMailRequest & DraftMailRequest & MailMessage;
  await expect(operation(request, undefined as never, undefined as never)).rejects.toThrow("API kredisi tükendi");
});

it("distinguishes local service connectivity from provider authentication", async () => {
  vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
  expect((await getApiStatus()).detail).toContain("Windows hizmetini");
});

it("supports a previous plain-text health endpoint during an update", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Healthy")));
  expect((await getApiStatus()).state).toBe("healthy");
});
