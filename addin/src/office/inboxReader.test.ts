import { describe, expect, it } from "vitest";
import { mapGraphMessages } from "./inboxReader";

describe("inbox reader", () => {
  it("maps a Graph inbox message into the triage contract", () => {
    const messages = mapGraphMessages([{
      id: "id-1",
      conversationId: "conv-1",
      subject: "Bütçe onayı",
      receivedDateTime: "2026-10-19T08:00:00Z",
      bodyPreview: "Yazılı onayınızı bekliyorum.",
      from: { emailAddress: { name: "Ayşe", address: "ayse@example.com" } },
      toRecipients: [{ emailAddress: { name: "Ali", address: "ali@example.com" } }],
      internetMessageHeaders: [{ name: "Auto-Submitted", value: "no" }],
    }]);

    expect(messages).toHaveLength(1);
    expect(messages[0]).toMatchObject({
      itemId: "id-1",
      conversationId: "conv-1",
      subject: "Bütçe onayı",
      from: { name: "Ayşe", address: "ayse@example.com" },
      receivedAt: "2026-10-19T08:00:00Z",
      hasUnsubscribeHint: false,
      isAutoSubmitted: false,
    });
  });

  it("marks newsletter headers", () => {
    const messages = mapGraphMessages([{
      id: "news-1",
      from: { emailAddress: { address: "news@example.com" } },
      internetMessageHeaders: [{ name: "List-Unsubscribe", value: "<mailto:leave@example.com>" }],
    }]);

    expect(messages[0]?.hasUnsubscribeHint).toBe(true);
  });
});
