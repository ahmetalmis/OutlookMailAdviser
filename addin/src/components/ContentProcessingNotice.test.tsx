import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { ContentProcessingNotice } from "./ContentProcessingNotice";

const processing = {
  originalCharacters: 20000, includedCharacters: 5000, includedHistoryMessages: 2,
  historyLimited: true, currentMessageTruncated: false, parsingUncertain: false,
};

describe("ContentProcessingNotice", () => {
  it("shows normal history reduction without a warning", () => {
    const html = renderToStaticMarkup(<ContentProcessingNotice processing={processing} wasTruncated />);
    expect(html).toContain("önceki 2 mesaj");
    expect(html).not.toContain('class="warning"');
  });
  it("warns when the current message was cut", () => {
    const html = renderToStaticMarkup(<ContentProcessingNotice processing={{ ...processing, currentMessageTruncated: true }} wasTruncated />);
    expect(html).toContain("İçeriğin bir bölümü analize dahil edilemedi.");
  });
  it("supports older responses", () => {
    expect(renderToStaticMarkup(<ContentProcessingNotice wasTruncated />)).toContain("Kaynak içerik kısaltıldı.");
  });
});
