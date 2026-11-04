import { afterEach, describe, expect, it, vi } from "vitest";
import { analyzeMail, draftMail, getApiStatus, MailAnalysisApiError } from "./mailAnalysisApi";
import type { AnalyzeMailRequest, DraftMailRequest } from "../types";

const request: AnalyzeMailRequest = {
  clientRequestId: "11111111-1111-4111-8111-111111111111",
  preferredLanguage: "tr",
  message: {
    itemId: "item-1",
    subject: "Test",
    from: { name: "Ayşe", address: "ayse@example.com" },
    to: [{ name: "Ali", address: "ali@example.com" }],
    cc: [],
    sentAt: "2026-09-20T10:00:00.000Z",
    body: "Merhaba",
    bodyFormat: "html",
  },
};

afterEach(() => vi.unstubAllGlobals());

describe("analyzeMail", () => {
  it("posts the Outlook message to the analysis endpoint", async () => {
    const responseBody = { clientRequestId: request.clientRequestId, summary: "Özet" };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(responseBody), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await analyzeMail(request);

    expect(result.summary).toBe("Özet");
    expect(fetchMock).toHaveBeenCalledWith(
      "https://localhost:7047/api/v1/mail/analysis",
      expect.objectContaining({ method: "POST", body: JSON.stringify(request) }),
    );
  });

  it("surfaces ProblemDetails code and detail", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      code: "invalid_model_response",
      detail: "Model output is invalid.",
    }), { status: 502, headers: { "Content-Type": "application/json" } })));

    await expect(analyzeMail(request)).rejects.toEqual(
      expect.objectContaining<Partial<MailAnalysisApiError>>({
        message: "Model output is invalid.",
        status: 502,
        code: "invalid_model_response",
      }),
    );
  });
});

describe("getApiStatus", () => {
  it("accepts ASP.NET Core's plain-text Healthy response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Healthy", {
      status: 200,
      headers: { "Content-Type": "text/plain" },
    })));

    await expect(getApiStatus()).resolves.toEqual({ state: "healthy" });
  });
});

describe("draftMail", () => {
  it("posts tone, instructions, and the current message to the draft endpoint", async () => {
    const draftRequest: DraftMailRequest = {
      ...request,
      tone: "friendly",
      toneDetails: "Hafif sert ve uyarıcı olsun.",
      instructions: "Tarihi teyit et ve teşekkür et.",
    };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      clientRequestId: request.clientRequestId,
      subject: "Re: Test",
      body: "Teşekkürler.",
      tone: "friendly",
      model: "test-model",
      durationMilliseconds: 20,
      wasTruncated: false,
    }), { status: 200, headers: { "Content-Type": "application/json" } }));
    vi.stubGlobal("fetch", fetchMock);

    const result = await draftMail(draftRequest);

    expect(result.subject).toBe("Re: Test");
    expect(fetchMock).toHaveBeenCalledWith(
      "https://localhost:7047/api/v1/mail/draft",
      expect.objectContaining({ method: "POST", body: JSON.stringify(draftRequest) }),
    );
  });
});
