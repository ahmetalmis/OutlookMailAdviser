import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import App from "../App";
import { analyzeMail, askMailQuestion, draftMail, getApiStatus } from "../api/mailAnalysisApi";
import { createAnalysisRequest, readCurrentMessage } from "../office/outlookItemReader";
import { en, tr, type Language } from "./messages";
import { UI_LANGUAGE_KEY } from "./LanguageContext";
import type { AnalyzeMailResponse, MailMessage } from "../types";

vi.mock("../office/outlookItemReader", () => ({ readCurrentMessage: vi.fn(), createAnalysisRequest: vi.fn(), registerItemChanged: vi.fn() }));
vi.mock("../api/mailAnalysisApi", () => ({ analyzeMail: vi.fn(), askMailQuestion: vi.fn(), draftMail: vi.fn(), getApiStatus: vi.fn() }));
const message: MailMessage = { itemId: "one", subject: "Original subject", from: { name: "Sender", address: "a@b.com" }, to: [{ name: null, address: "b@b.com" }], cc: [], sentAt: null, body: "Original quote", bodyFormat: "plainText" };
const analysis: AnalyzeMailResponse = { clientRequestId: "id", summary: "Original summary", actionRequired: true, actionRequiredFromCurrentUser: true, actions: [], priority: "high", sentiment: "neutral", warnings: ["Original model warning", en.contentOmitted, tr.contentOmitted], model: "test", durationMilliseconds: 1200, wasTruncated: true, contentProcessing: { originalCharacters: 6000, includedCharacters: 5000, includedHistoryMessages: 2, historyLimited: true, currentMessageTruncated: true, parsingUncertain: true } };
let root: Root; let container: HTMLDivElement;
beforeEach(() => {
  vi.clearAllMocks(); localStorage.clear(); Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.mocked(readCurrentMessage).mockResolvedValue(message);
  vi.mocked(createAnalysisRequest).mockImplementation(async (language = "tr") => ({ clientRequestId: "id", preferredLanguage: language, message }));
  vi.mocked(getApiStatus).mockResolvedValue({ state: "healthy" });
  vi.mocked(analyzeMail).mockResolvedValue(analysis);
  vi.mocked(askMailQuestion).mockResolvedValue({ answer: "Original answer", sources: [{ id: "s1", quote: "Original quote", startOffset: 0 }], completedChunks: 1, totalChunks: 2, isPartial: true, warnings: ["Original question warning"] });
  vi.mocked(draftMail).mockResolvedValue({ clientRequestId: "id", subject: "Original draft subject", body: "Original draft", tone: "professional", model: "test", durationMilliseconds: 1200, wasTruncated: false });
  container = document.createElement("div"); document.body.append(container); root = createRoot(container);
});
afterEach(async () => { await act(async () => root.unmount()); container.remove(); localStorage.clear(); vi.restoreAllMocks(); });
async function change(element: HTMLSelectElement | HTMLTextAreaElement, value: string) {
  await act(async () => {
    Object.getOwnPropertyDescriptor(element instanceof HTMLSelectElement ? HTMLSelectElement.prototype : HTMLTextAreaElement.prototype, "value")!.set!.call(element, value);
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
  });
}
async function click(text: string) { await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === text)!.click()); }
it.each<Language>(["en", "tr"])("keeps %s UI independent from output and preserves results, fields and sources on a UI switch", async ui => {
  const output: Language = ui === "en" ? "tr" : "en";
  localStorage.setItem(UI_LANGUAGE_KEY, ui); localStorage.setItem("mail-adviser.output-language", output);
  const strings = ui === "tr" ? tr : en; const next = ui === "tr" ? en : tr;
  await act(async () => root.render(<App />));
  await click(strings.analyze);
  expect(analyzeMail).toHaveBeenCalledWith(expect.objectContaining({ preferredLanguage: output }));
  await change(container.querySelector(".mail-questions textarea")!, "Keep this question");
  await click(strings.ask);
  expect(askMailQuestion).toHaveBeenCalledWith(message, "Keep this question", output, expect.any(AbortSignal));
  await act(async () => container.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click());
  await click(strings.useSources);
  await change(container.querySelectorAll<HTMLTextAreaElement>(".draft-composer textarea")[0], "Keep my instructions");
  await click(strings.createDraft);
  expect(draftMail).toHaveBeenCalledWith(expect.objectContaining({ preferredLanguage: output, sourceQuotes: ["Original quote"], instructions: "Keep my instructions" }));
  const calls = [vi.mocked(readCurrentMessage).mock.calls.length, vi.mocked(getApiStatus).mock.calls.length];
  await change(container.querySelector<HTMLSelectElement>(".language-field select")!, output);
  expect(container.textContent).toContain(next.analysisResult);
  for (const text of ["Original summary", "Original answer", "Original quote", "Original draft", "Original model warning", "Original question warning"]) expect(container.textContent).toContain(text);
  expect(container.querySelector<HTMLInputElement>('input[type="checkbox"]')!.checked).toBe(true);
  expect(container.querySelector<HTMLTextAreaElement>(".mail-questions textarea")!.value).toBe("Keep this question");
  expect(container.querySelector<HTMLTextAreaElement>(".draft-composer textarea")!.value).toBe("Keep my instructions");
  expect(container.querySelector(".selected-sources")).not.toBeNull();
  expect(container.textContent).toContain(next.contentOmitted);
  expect([...container.querySelectorAll(".warning")].filter(node => node.textContent === next.contentOmitted)).toHaveLength(1);
  expect(container.textContent).toContain(output === "en" ? "5,000" : "5.000");
  expect(analyzeMail).toHaveBeenCalledTimes(1); expect(askMailQuestion).toHaveBeenCalledTimes(1); expect(draftMail).toHaveBeenCalledTimes(1);
  expect([vi.mocked(readCurrentMessage).mock.calls.length, vi.mocked(getApiStatus).mock.calls.length]).toEqual(calls);
  expect(container.querySelectorAll<HTMLSelectElement>(".language-field select")[1].value).toBe(output);
  const writeText = vi.fn().mockResolvedValue(undefined); Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText } });
  await click(next.copy); expect(writeText).toHaveBeenCalledWith("Original draft"); expect(container.textContent).toContain(next.copied);
});
it("retranslates existing API and Office errors without retrying operations", async () => {
  localStorage.setItem(UI_LANGUAGE_KEY, "tr");
  vi.mocked(readCurrentMessage).mockRejectedValue({ code: "openEmail" });
  vi.mocked(getApiStatus).mockResolvedValue({ state: "unhealthy", code: "openai_authentication_failed", detail: "PRIVATE_KEY" });
  await act(async () => root.render(<App />));
  expect(container.textContent).toContain(tr.openEmail);
  await change(container.querySelector<HTMLSelectElement>(".language-field select")!, "en");
  expect(container.textContent).toContain(en.openEmail); expect(container.textContent).toContain(en.openai_authentication_failed); expect(container.textContent).not.toContain("PRIVATE_KEY");
  expect(readCurrentMessage).toHaveBeenCalledTimes(1); expect(getApiStatus).toHaveBeenCalledTimes(1);
});
it("updates an in-flight question's controls without cancelling it, then translates cancellation", async () => {
  localStorage.setItem(UI_LANGUAGE_KEY, "tr");
  let finish!: (value: Awaited<ReturnType<typeof askMailQuestion>>) => void;
  vi.mocked(askMailQuestion).mockReturnValue(new Promise(resolve => { finish = resolve; }));
  await act(async () => root.render(<App />));
  await change(container.querySelector<HTMLTextAreaElement>(".mail-questions textarea")!, "Question"); await click(tr.ask);
  const signal = vi.mocked(askMailQuestion).mock.calls[0][3]!;
  await change(container.querySelector<HTMLSelectElement>(".language-field select")!, "en");
  expect(signal.aborted).toBe(false); expect(container.textContent).toContain(en.examiningHelp);
  await click(en.cancel); expect(signal.aborted).toBe(true); expect(container.textContent).toContain(en.cancelled);
  await change(container.querySelector<HTMLSelectElement>(".language-field select")!, "tr"); expect(container.textContent).toContain(tr.cancelled);
  await act(async () => finish({ answer: "Late answer", sources: [], completedChunks: 1, totalChunks: 1, isPartial: false, warnings: [] }));
  expect(container.textContent).not.toContain("Late answer");
});
