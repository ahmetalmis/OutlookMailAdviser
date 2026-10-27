import type { ActionNote, ActionNoteInput, NewActionNote } from "../types";

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV
  ? "https://localhost:7047" : window.location.origin)).replace(/\/$/, "");
export class ActionNoteApiError extends Error {
  constructor(public readonly code: string, public readonly status: number) { super(code); }
}
async function request(path: string, method: string, body?: unknown, signal?: AbortSignal): Promise<unknown> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}/api/v1/action-notes${path}`, {
      method, signal, headers: body === undefined ? undefined : { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new ActionNoteApiError("local_unavailable", 0);
  }
  if (!response.ok) {
    let code = "action_note_request_error";
    try { const problem = await response.json(); if (typeof problem.code === "string") code = problem.code; } catch { /* Safe generic message. */ }
    throw new ActionNoteApiError(code, response.status);
  }
  return response.json();
}
export const listActionNotes = (signal?: AbortSignal) => request("", "GET", undefined, signal) as Promise<ActionNote[]>;
export const createActionNote = (note: NewActionNote) => request("", "POST", note) as Promise<ActionNote>;
export const editActionNote = (id: string, note: ActionNoteInput) => request(`/${encodeURIComponent(id)}`, "PUT", note) as Promise<ActionNote>;
export const completeActionNote = (id: string, completed: boolean) => request(`/${encodeURIComponent(id)}/completion`, "PATCH", { completed }) as Promise<ActionNote>;
