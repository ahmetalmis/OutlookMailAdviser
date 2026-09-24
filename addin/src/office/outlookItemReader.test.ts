import { beforeEach, describe, expect, it, vi } from "vitest";
import { createAnalysisRequest, readCurrentMessage } from "./outlookItemReader";

function officeWithItem(
  item: Record<string, unknown>,
  userProfile?: { displayName: string; emailAddress: string },
) {
  return {
    CoercionType: { Html: "html" },
    AsyncResultStatus: { Succeeded: "succeeded" },
    EventType: { ItemChanged: "itemChanged" },
    MailboxEnums: { ItemType: { Message: "message" } },
    context: {
      displayLanguage: "tr-TR",
      mailbox: { item, userProfile, addHandlerAsync: vi.fn() },
    },
  };
}

beforeEach(() => {
  vi.stubGlobal("Office", officeWithItem({}));
});

describe("Outlook item reader", () => {
  it("maps the current read-mode message to the API contract", async () => {
    const item = {
      itemType: "message",
      itemId: "item-42",
      subject: "Üretim geçiş planı",
      from: { displayName: "Ayşe", emailAddress: "ayse@example.com" },
      to: [{ displayName: "Ali", emailAddress: "ali@example.com" }],
      cc: [],
      dateTimeCreated: new Date("2026-09-20T10:00:00Z"),
      body: {
        getAsync: vi.fn((_coercionType, callback) => callback({
          status: "succeeded",
          value: "<p>Tarihi teyit eder misin?</p>",
        })),
      },
    };
    vi.stubGlobal("Office", officeWithItem(item));

    const message = await readCurrentMessage();

    expect(message).toEqual(expect.objectContaining({
      itemId: "item-42",
      subject: "Üretim geçiş planı",
      bodyFormat: "html",
      body: "<p>Tarihi teyit eder misin?</p>",
    }));
    expect(message.from.address).toBe("ayse@example.com");
  });

  it("creates a Turkish request by default", async () => {
    const item = {
      itemType: "message",
      subject: "Test",
      from: { displayName: "Ayşe", emailAddress: "ayse@example.com" },
      to: [{ displayName: "Ali", emailAddress: "ali@example.com" }],
      cc: [],
      body: { getAsync: vi.fn((_type, callback) => callback({ status: "succeeded", value: "Test" })) },
    };
    vi.stubGlobal("Office", officeWithItem(item));

    const request = await createAnalysisRequest();

    expect(request.preferredLanguage).toBe("tr");
    expect(request.clientRequestId).toBe("11111111-1111-4111-8111-111111111111");
    expect(request.currentUser).toBeUndefined();
  });

  it("uses the language explicitly selected in the task pane", async () => {
    const item = {
      itemType: "message",
      subject: "Test",
      from: { displayName: "Ayşe", emailAddress: "ayse@example.com" },
      to: [{ displayName: "Ali", emailAddress: "ali@example.com" }],
      cc: [],
      body: { getAsync: vi.fn((_type, callback) => callback({ status: "succeeded", value: "Test" })) },
    };
    vi.stubGlobal("Office", officeWithItem(item));

    const request = await createAnalysisRequest("en");

    expect(request.preferredLanguage).toBe("en");
  });

  it("adds the Outlook user profile to the analysis request", async () => {
    const item = {
      itemType: "message",
      subject: "Test",
      from: { displayName: "Ayşe", emailAddress: "ayse@example.com" },
      to: [{ displayName: "Ali", emailAddress: "ali@example.com" }],
      cc: [],
      body: { getAsync: vi.fn((_type, callback) => callback({ status: "succeeded", value: "Test" })) },
    };
    vi.stubGlobal("Office", officeWithItem(item, {
      displayName: "Ali Yılmaz",
      emailAddress: "ali@example.com",
    }));

    const request = await createAnalysisRequest("tr");

    expect(request.currentUser).toEqual({
      name: "Ali Yılmaz",
      address: "ali@example.com",
    });
  });
});
