import { act, useCallback, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { ActionNotes } from "./ActionNotes";
import { completeActionNote, createActionNote, editActionNote, listActionNotes } from "../api/actionNotesApi";
import { LanguageProvider } from "../i18n/LanguageContext";
import { en, tr } from "../i18n/messages";
import type { ActionNote, NewActionNote } from "../types";

vi.mock("../api/actionNotesApi", () => ({ listActionNotes: vi.fn(), createActionNote: vi.fn(), editActionNote: vi.fn(), completeActionNote: vi.fn() }));
const selection: NewActionNote = { id: "11111111-1111-4111-8111-111111111111", mailSubject: "Mail title", description: "Confirm delivery", owner: "Alex", dueDate: "2026-11-02" };
let records: ActionNote[];
let container: HTMLDivElement; let root: Root;
function saved(input: NewActionNote): ActionNote { return { ...input, createdAt: "2026-10-27T10:00:00Z", updatedAt: "2026-10-27T10:00:00Z", completedAt: null, isCompleted: false }; }
function Harness({ initial = null }: { initial?: NewActionNote | null }) {
  const [open, setOpen] = useState(true); const [selected, setSelected] = useState(initial);
  const clear = useCallback(() => setSelected(null), []);
  return <LanguageProvider><ActionNotes open={open} onOpenChange={setOpen} selection={selected} onSelectionClear={clear} /></LanguageProvider>;
}
beforeEach(() => {
  vi.clearAllMocks(); records = []; localStorage.setItem("mail-adviser.ui-language", "tr");
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.mocked(listActionNotes).mockImplementation(async () => records.map(note => ({ ...note })));
  vi.mocked(createActionNote).mockImplementation(async note => { const result = saved(note); records.push(result); return result; });
  vi.mocked(editActionNote).mockImplementation(async (id, input) => { const index = records.findIndex(note => note.id === id); records[index] = { ...records[index], ...input }; return records[index]; });
  vi.mocked(completeActionNote).mockImplementation(async (id, completed) => { const note = records.find(note => note.id === id)!; note.isCompleted = completed; note.completedAt = completed ? "2026-10-27T11:00:00Z" : null; return { ...note }; });
  container = document.createElement("div"); document.body.append(container); root = createRoot(container);
});
afterEach(async () => { await act(async () => root.unmount()); container.remove(); localStorage.clear(); vi.restoreAllMocks(); });
async function click(text: string) { await act(async () => [...container.querySelectorAll("button")].find(button => button.textContent === text)!.click()); }
async function change(element: HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement, value: string) {
  await act(async () => {
    const prototype = element instanceof HTMLSelectElement ? HTMLSelectElement.prototype : element instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    Object.getOwnPropertyDescriptor(prototype, "value")!.set!.call(element, value);
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
  });
}
it("reviews populated fields and saves edited text with an unknown owner", async () => {
  await act(async () => root.render(<Harness initial={selection} />));
  expect(container.querySelector<HTMLInputElement>("form input")!.value).toBe("Mail title");
  await change(container.querySelector<HTMLTextAreaElement>("form textarea")!, "Edited action");
  await change(container.querySelectorAll<HTMLInputElement>("form input")[1], " ");
  await click(tr.saveNote);
  expect(createActionNote).toHaveBeenCalledWith({ ...selection, description: "Edited action", owner: null });
  expect(container.textContent).toContain(tr.noteSaved); expect(container.textContent).toContain(tr.unknownOwner);
  expect(container.querySelector("form")).toBeNull();
});
it("suppresses duplicate submits and keeps the same ID after an uncertain save failure", async () => {
  let fail!: (reason: unknown) => void;
  vi.mocked(createActionNote).mockReturnValueOnce(new Promise((_, reject) => { fail = reject; }));
  await act(async () => root.render(<Harness initial={selection} />));
  await act(async () => { const form = container.querySelector("form")!; form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true })); });
  expect(createActionNote).toHaveBeenCalledTimes(1);
  expect(container.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  await act(async () => fail({ code: "local_unavailable" }));
  expect(container.textContent).toContain(tr.local_unavailable);
  await click(tr.saveNote);
  expect(vi.mocked(createActionNote).mock.calls.map(call => call[0].id)).toEqual([selection.id, selection.id]);
});
it("sorts, searches, filters, edits, completes and reopens saved actions", async () => {
  records = [saved(selection), { ...saved({ ...selection, id: "two", mailSubject: "Another mail", description: "Newer task", owner: null }), createdAt: "2026-10-27T11:00:00Z" }];
  await act(async () => root.render(<Harness />));
  expect(container.querySelector("li h3")!.textContent).toBe("Another mail");
  await change(container.querySelector<HTMLInputElement>('input[type="search"]')!, "delivery");
  expect(container.querySelectorAll("li")).toHaveLength(1);
  await change(container.querySelector<HTMLInputElement>('input[type="search"]')!, "");
  await change(container.querySelector<HTMLSelectElement>("select")!, JSON.stringify("Alex"));
  expect(container.querySelectorAll("li")).toHaveLength(1);
  await click(tr.editNote);
  await change(container.querySelector<HTMLTextAreaElement>("form textarea")!, "Updated task"); await click(tr.saveNote);
  expect(records[0].description).toBe("Updated task");
  await change(container.querySelector<HTMLSelectElement>("select")!, JSON.stringify("Alex"));
  await click(tr.completeNote);
  expect(container.querySelectorAll("li")).toHaveLength(0);
  await click(`${tr.completedNotes} (1)`);
  expect(container.querySelectorAll("li")).toHaveLength(1);
  await click(tr.reopenNote);
  expect(records[0].isCompleted).toBe(false);
  await click(`${tr.activeNotes} (2)`); expect(container.querySelectorAll("li")).toHaveLength(2);
});
it("shows storage errors without claiming an empty store, and can retry", async () => {
  vi.mocked(listActionNotes).mockRejectedValueOnce({ code: "action_note_storage_error", message: "PRIVATE_PATH" });
  await act(async () => root.render(<Harness />));
  expect(container.textContent).toContain(tr.action_note_storage_error);
  expect(container.textContent).not.toContain(tr.noActionNotes); expect(container.textContent).not.toContain("PRIVATE_PATH");
  await click(tr.refreshNotes); expect(container.textContent).toContain(tr.noActionNotes);
});
it("renders English forms and list labels without translating saved content", async () => {
  localStorage.setItem("mail-adviser.ui-language", "en"); records = [saved(selection)];
  await act(async () => root.render(<Harness initial={selection} />));
  expect(container.textContent).toContain(en.newActionNote); expect(container.textContent).toContain(en.myActions);
  await click(en.cancel); expect(container.querySelector("li")!.textContent).toContain(selection.description);
  await click(en.completeNote); expect(container.textContent).toContain(en.noteCompleted);
});
