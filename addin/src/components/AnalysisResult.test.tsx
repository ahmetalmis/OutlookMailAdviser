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

  it("does not highlight actions assigned to someone else", () => {
    const html = renderToStaticMarkup(<AnalysisResult result={createResult(false)} />);

    expect(html).not.toContain("Senden aksiyon bekleniyor");
    expect(html).not.toContain("class=\"action-mine\"");
  });
});
