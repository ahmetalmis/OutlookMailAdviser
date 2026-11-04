import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import type { AnalyzeMailResponse } from "../types";
import { AnalysisResult } from "./AnalysisResult";

function createResult(assignedToCurrentUser: boolean): AnalyzeMailResponse {
  return {
    clientRequestId: "11111111-1111-4111-8111-111111111111",
    summary: "Tarih teyidi isteniyor.",
    actionRequired: true,
    actionRequiredFromCurrentUser: assignedToCurrentUser,
    actions: [{
      description: "Tarihi teyit et",
      owner: assignedToCurrentUser ? "Ali" : "Ayşe",
      dueDate: null,
      confidence: 0.9,
      assignedToCurrentUser,
    }],
    priority: "high",
    sentiment: "neutral",
    warnings: [],
    model: "test-model",
    durationMilliseconds: 10,
    wasTruncated: false,
  };
}

describe("AnalysisResult", () => {
  it("highlights actions assigned to the current user", () => {
    const html = renderToStaticMarkup(<AnalysisResult result={createResult(true)} />);

    expect(html).toContain("Senden aksiyon bekleniyor");
    expect(html).toContain("class=\"action-mine\"");
    expect(html).toContain("Senden bekleniyor");
  });

  it("shows the original mail beside the summary", () => {
    const html = renderToStaticMarkup(
      <AnalysisResult
        result={createResult(false)}
        message={{
          itemId: "mail-1",
          subject: "Toplantı",
          from: { name: "Meryem", address: "meryem@innova.com.tr" },
          to: [{ name: "Kamil", address: "ozozdemir@innova.com.tr" }],
          cc: [],
          sentAt: null,
          body: "<p>Herkes müsaitse bugün öğleden sonra toplantı yapalım mı?</p>",
          bodyFormat: "html",
        }}
      />,
    );

    expect(html).toContain("Ana mail");
    expect(html).toContain("Herkes müsaitse bugün öğleden sonra toplantı yapalım mı?");
    expect(html).toContain("Tarih teyidi isteniyor.");
  });

  it("does not highlight actions assigned to someone else", () => {
    const html = renderToStaticMarkup(<AnalysisResult result={createResult(false)} />);

    expect(html).not.toContain("Senden aksiyon bekleniyor");
    expect(html).not.toContain("class=\"action-mine\"");
  });
});
