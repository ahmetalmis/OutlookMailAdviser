import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { initialLanguage, LanguageProvider, readPreference, savePreference, UI_LANGUAGE_KEY, useLanguage } from "./LanguageContext";
import { en, errorText, tr, translator, UiError } from "./messages";

let root: Root;
let container: HTMLDivElement;
beforeEach(() => {
  localStorage.clear();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.stubGlobal("Office", { context: { displayLanguage: "tr-TR" } });
  container = document.createElement("div"); document.body.append(container); root = createRoot(container);
});
afterEach(async () => { await act(async () => root.unmount()); container.remove(); vi.restoreAllMocks(); vi.unstubAllGlobals(); localStorage.clear(); });
it("has matching dictionary keys and interpolation placeholders", () => {
  expect(Object.keys(en).sort()).toEqual(Object.keys(tr).sort());
  for (const key of Object.keys(tr) as (keyof typeof tr)[]) {
    expect(en[key].match(/\{\w+\}/g)?.sort()).toEqual(tr[key].match(/\{\w+\}/g)?.sort());
  }
});
it("prefers stored choices, then Outlook, then browser; unsupported languages use English", () => {
  expect(initialLanguage()).toBe("tr");
  localStorage.setItem(UI_LANGUAGE_KEY, "en"); expect(initialLanguage()).toBe("en");
  localStorage.setItem(UI_LANGUAGE_KEY, "invalid"); expect(initialLanguage()).toBe("tr");
  vi.stubGlobal("Office", { context: { displayLanguage: "de-DE" } }); expect(initialLanguage()).toBe("en");
  vi.stubGlobal("Office", undefined);
  vi.spyOn(navigator, "language", "get").mockReturnValue("tr-TR"); expect(initialLanguage()).toBe("tr");
  vi.spyOn(navigator, "language", "get").mockReturnValue("fr-FR"); expect(initialLanguage()).toBe("en");
});
it("continues working when storage reads and writes fail", async () => {
  vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("denied"); });
  vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("denied"); });
  expect(readPreference(UI_LANGUAGE_KEY)).toBeNull(); expect(() => savePreference(UI_LANGUAGE_KEY, "en")).not.toThrow();
  await act(async () => root.render(<LanguageProvider><Probe /></LanguageProvider>));
  await act(async () => container.querySelector("button")!.click());
  expect(container.textContent).toContain("Create draft 5,000 1.2"); expect(document.documentElement.lang).toBe("en");
});
function Probe() {
  const { t, number, setLanguage } = useLanguage();
  return <button onClick={() => setLanguage("en")}>{t("createDraft")} {number(5000)} {number(1.2, 1)}</button>;
}
it("persists manual choices and updates document language and number formatting", async () => {
  await act(async () => root.render(<LanguageProvider><Probe /></LanguageProvider>));
  expect(container.textContent).toContain("Taslak oluştur 5.000 1,2");
  await act(async () => container.querySelector("button")!.click());
  expect(localStorage.getItem(UI_LANGUAGE_KEY)).toBe("en"); expect(document.documentElement.lang).toBe("en");
});
it("renders stored error codes in the current language and never exposes raw error details", () => {
  const error = new UiError("staleSources");
  expect(errorText(error, translator("tr"))).toBe(tr.staleSources);
  expect(errorText(error, translator("en"))).toBe(en.staleSources);
  expect(errorText({ code: "new_provider_error", message: "PRIVATE_KEY" }, translator("en"))).toBe(en.genericError + " (new_provider_error)");
  expect(errorText(new Error("PRIVATE_KEY"), translator("en"))).toBe(en.genericError);
});
