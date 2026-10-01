import { act } from "react";
import { createRoot } from "react-dom/client";
import { expect, it, vi } from "vitest";
import App from "./App";
import { draftMail, getApiStatus } from "./api/mailAnalysisApi";
import { createAnalysisRequest, registerItemChanged } from "./office/outlookItemReader";

vi.mock("./office/outlookItemReader", () => {
  const message = { itemId: "one", subject: "Test", from: { name: null, address: "a@b.com" },
    to: [{ name: null, address: "b@b.com" }], cc: [], sentAt: null, body: "Original", bodyFormat: "plainText" };
  return {
    readCurrentMessage: vi.fn(async () => message),
    registerItemChanged: vi.fn(),
    createAnalysisRequest: vi.fn(async () => ({ clientRequestId: "id", preferredLanguage: "tr", message: { ...message, body: "Changed" } })),
  };
});
vi.mock("./api/mailAnalysisApi", () => ({
  getApiStatus: vi.fn(async () => ({ state: "healthy" })),
  analyzeMail: vi.fn(), draftMail: vi.fn(), MailAnalysisApiError: class extends Error {},
}));
vi.mock("./components/MailQuestions", () => ({
  MailQuestions: ({ onUseSources }: { onUseSources: (quotes: string[]) => void }) =>
    <button onClick={() => onUseSources(["Original"])}>Taslakta kullan</button>,
}));

it("keeps the draft composer and its error visible when stale source quotes are cleared", async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  try {
    await act(async () => root.render(<App />));
    await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === "Taslakta kullan")!.click());
    await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === "Taslak oluştur")!.click());
    expect(container.textContent).toContain("E-posta içeriği değişti. Kaynakları yeniden seçin.");
    expect(container.querySelector(".draft-composer")).not.toBeNull();
    expect(container.querySelector(".selected-sources")).toBeNull();
    expect(draftMail).not.toHaveBeenCalled();
  } finally {
    await act(async () => root.unmount());
    container.remove();
  }
});

it("shows the provider failure at startup and lets the user recheck after updating the key", async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.mocked(getApiStatus).mockResolvedValueOnce({ state: "unhealthy", code: "openai_authentication_failed",
    detail: "OpenAI API anahtarı/token kabul edilmedi. Hizmette tanımlı anahtarı güncelleyin." });
  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  try {
    await act(async () => root.render(<App />));
    expect(container.querySelector('[role="status"]')?.textContent).toContain("API anahtarı/token kabul edilmedi");
    vi.mocked(getApiStatus).mockResolvedValueOnce({ state: "healthy", provider: "OpenAI", model: "test-model" });
    await act(async () => container.querySelector<HTMLButtonElement>(".status-retry")!.click());
    expect(container.querySelector('[role="status"]')?.textContent).toContain("API hazır · OpenAI / test-model");
  } finally {
    await act(async () => root.unmount());
    container.remove();
  }
});

it("copies only the reply body and leaves out the subject", async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const writeText = vi.fn(async () => undefined);
  Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText } });
  vi.mocked(draftMail).mockResolvedValueOnce({
    clientRequestId: "draft-id",
    subject: "RE: Test",
    body: "Yalnızca yanıt metni",
    tone: "professional",
    model: "test-model",
    durationMilliseconds: 100,
    wasTruncated: false,
  });
  vi.mocked(createAnalysisRequest).mockResolvedValueOnce({
    clientRequestId: "id",
    preferredLanguage: "tr",
    message: {
      itemId: "one",
      subject: "Test",
      from: { name: null, address: "a@b.com" },
      to: [{ name: null, address: "b@b.com" }],
      cc: [],
      sentAt: null,
      body: "Original",
      bodyFormat: "plainText",
    },
  });

  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  try {
    await act(async () => root.render(<App />));
    await act(async () => [...container.querySelectorAll("button")]
      .find(button => button.textContent === "Taslakta kullan")!.click());
    await act(async () => container.querySelector<HTMLButtonElement>(".draft-button")!.click());
    await act(async () => [...container.querySelectorAll("button")]
      .find(button => button.textContent === "Kopyala")!.click());

    expect(writeText).toHaveBeenCalledWith("Yalnızca yanıt metni");
    expect(writeText).not.toHaveBeenCalledWith(expect.stringContaining("RE: Test"));
  } finally {
    await act(async () => root.unmount());
    container.remove();
  }
});

async function changeField(element: HTMLTextAreaElement | HTMLSelectElement, value: string) {
  await act(async () => {
    const prototype = element instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLTextAreaElement.prototype;
    Object.getOwnPropertyDescriptor(prototype, "value")!.set!.call(element, value);
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
  });
}

it("validates the forward audience, sends separate constraints, and resets them on item change", async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.mocked(draftMail).mockClear();
  const container = document.createElement("div");
  document.body.append(container);
  const root = createRoot(container);
  const field = (text: string) => [...container.querySelectorAll("label")]
    .find(label => label.querySelector("span")?.textContent?.startsWith(text))!
    .querySelector<HTMLTextAreaElement | HTMLSelectElement>("textarea, select")!;
  const click = async (text: string) => act(async () => {
    [...container.querySelectorAll("button")].find(button => button.textContent === text)!.click();
  });
  try {
    await act(async () => root.render(<App />));
    await click("Taslakta kullan");
    await click("Kaynakları kaldır");
    expect(field("Yazışma türü").value).toBe("reply");
    expect(container.textContent).not.toContain("Kime yazılacak?");
    await changeField(field("Yazışma türü"), "forward");
    expect((field("Kime yazılacak?") as HTMLTextAreaElement).maxLength).toBe(500);
    expect((field("Dikkat edilecek hususlar") as HTMLTextAreaElement).maxLength).toBe(2000);
    expect((field("Kime yazılacak?") as HTMLTextAreaElement).required).toBe(true);
    await changeField(field("Kime yazılacak?"), "   ");
    expect(container.querySelector<HTMLButtonElement>(".draft-button")!.disabled).toBe(true);
    await click("Taslak oluştur");
    expect(draftMail).not.toHaveBeenCalled();
    await changeField(field("Kime yazılacak?"), "  Grup müdürüm  ");
    await changeField(field("Dikkat edilecek hususlar"), "  Tarih taahhüdü verme.  ");
    await changeField(field("Mailin amacı"), "Yönetimden aksiyon iste.");
    vi.mocked(draftMail).mockResolvedValue({ clientRequestId: "id", subject: "Geri bildirim", body: "Merhaba",
      tone: "professional", model: "test", durationMilliseconds: 1, wasTruncated: false });
    await click("Taslak oluştur");
    expect(draftMail).toHaveBeenLastCalledWith(expect.objectContaining({ draftMode: "forward",
      targetAudience: "Grup müdürüm", considerations: "Tarih taahhüdü verme.", instructions: "Yönetimden aksiyon iste." }));
    await changeField(field("Yazışma türü"), "reply");
    await click("Taslak oluştur");
    expect(draftMail).toHaveBeenLastCalledWith(expect.objectContaining({ draftMode: "reply", targetAudience: undefined }));
    await changeField(field("Yazışma türü"), "forward");
    const onItemChanged = vi.mocked(registerItemChanged).mock.calls.at(-1)![0];
    await act(async () => onItemChanged());
    await click("Taslakta kullan");
    expect(field("Yazışma türü").value).toBe("reply");
    expect(field("Dikkat edilecek hususlar").value).toBe("");
    expect(field("Mailin amacı").value).not.toBe("Yönetimden aksiyon iste.");
    await changeField(field("Yazışma türü"), "forward");
    expect(field("Kime yazılacak?").value).toBe("");
  } finally {
    await act(async () => root.unmount());
    container.remove();
  }
});
