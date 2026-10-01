import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MailQuestions } from "./MailQuestions";
import { askMailQuestion } from "../api/mailAnalysisApi";
import type { MailMessage, MailQuestionResponse } from "../types";

vi.mock("../api/mailAnalysisApi", () => ({ askMailQuestion: vi.fn() }));
const message: MailMessage = { itemId: "one", subject: "Test", from: { name: null, address: "a@b.com" },
  to: [{ name: null, address: "b@b.com" }], cc: [], sentAt: null, body: "Teslim 12 Ekim.", bodyFormat: "plainText" };
const response: MailQuestionResponse = { answer: "Teslim 12 Ekim.", sources: [{ id: "s1", quote: "Teslim 12 Ekim.", startOffset: 0 }],
  completedChunks: 1, totalChunks: 1, isPartial: false, warnings: [] };
let container: HTMLDivElement;
let root: Root;
const onUseSources = vi.fn();

beforeEach(async () => {
  vi.clearAllMocks();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
  await act(async () => root.render(<MailQuestions message={message} language="tr" onUseSources={onUseSources} />));
});
afterEach(async () => { await act(async () => root.unmount()); container.remove(); });

async function ask() {
  const textarea = container.querySelector("textarea")!;
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")!.set!.call(textarea, "Teslim ne zaman?");
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
  });
  await act(async () => { container.querySelector("button")!.click(); });
}

describe("MailQuestions", () => {
  it("asks without analysis and passes only selected quotes to drafting", async () => {
    vi.mocked(askMailQuestion).mockResolvedValue(response);
    await ask();
    expect(askMailQuestion).toHaveBeenCalledWith(message, "Teslim ne zaman?", "tr", expect.any(AbortSignal));
    expect(container.textContent).toContain("Kaynak alıntıları");
    await act(async () => (container.querySelector('input[type="checkbox"]') as HTMLInputElement).click());
    await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === "Taslakta kullan")!.click());
    expect(onUseSources).toHaveBeenCalledWith(["Teslim 12 Ekim."]);
  });

  it("cancels and ignores late answers", async () => {
    let finish!: (value: MailQuestionResponse) => void;
    vi.mocked(askMailQuestion).mockReturnValue(new Promise(resolve => { finish = resolve; }));
    await ask();
    const signal = vi.mocked(askMailQuestion).mock.calls[0][3]!;
    await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === "İptal et")!.click());
    expect(signal.aborted).toBe(true);
    await act(async () => finish(response));
    expect(container.textContent).not.toContain("Kaynak alıntıları");
    expect(container.textContent).toContain("iptal edildi");
  });

  it("discards the previous email's in-flight answer", async () => {
    let finish!: (value: MailQuestionResponse) => void;
    vi.mocked(askMailQuestion).mockReturnValue(new Promise(resolve => { finish = resolve; }));
    await ask();
    const signal = vi.mocked(askMailQuestion).mock.calls[0][3]!;
    await act(async () => root.render(<MailQuestions message={{ ...message, itemId: "two", body: "Başka mail" }}
      language="tr" onUseSources={onUseSources} />));
    expect(signal.aborted).toBe(true);
    await act(async () => finish(response));
    expect(container.textContent).not.toContain("Kaynak alıntıları");
  });

  it("shows incomplete coverage distinctly", async () => {
    vi.mocked(askMailQuestion).mockResolvedValue({ ...response, totalChunks: 4, isPartial: true,
      warnings: ["İnceleme tamamlanamadı."] });
    await ask();
    expect(container.textContent).toContain("1/4 bölüm incelendi · Kısmi sonuç");
    expect(container.textContent).toContain("İnceleme tamamlanamadı.");
  });
});
