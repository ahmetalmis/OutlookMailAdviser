import type { AnalyzeMailResponse, DraftTone, MailParticipant } from "../types";

const PRIORITY_KEY = "mail-adviser.priority-cache.v2";
const REPLIED_KEY = "mail-adviser.replied.v1";
const MESSAGE_KEY = "mail-adviser.message-cache.v1";
const REPLY_KEY = "mail-adviser.reply-drafts.v1";
const MAX_PRIORITY = 200;
const MAX_MESSAGES = 80;

export interface CachedPriorityMail {
  itemId: string;
  language: string;
  bucket: "action" | "review";
  subject: string;
  fromName: string | null;
  fromAddress: string;
  to?: MailParticipant[];
  cc?: MailParticipant[];
  receivedAt: string | null;
  summary: string;
  priority: string;
  read: boolean;
}

interface CachedMessageAnalysis {
  itemId: string;
  language: string;
  result: AnalyzeMailResponse;
}

export function loadPriorityCache(): CachedPriorityMail[] {
  return readJson<CachedPriorityMail[]>(PRIORITY_KEY) ?? [];
}

export function savePriorityCache(items: CachedPriorityMail[]): void {
  writeJson(PRIORITY_KEY, items.slice(-MAX_PRIORITY));
}

export function loadRepliedIds(): string[] {
  return readJson<string[]>(REPLIED_KEY) ?? [];
}

export function rememberReplied(itemId: string): void {
  const ids = loadRepliedIds().filter((id) => id !== itemId);
  ids.push(itemId);
  writeJson(REPLIED_KEY, ids.slice(-500));
  savePriorityCache(loadPriorityCache().filter((item) => item.itemId !== itemId));
}

export function loadMessageAnalysis(itemId: string, language: string): CachedMessageAnalysis | null {
  const stored = readJson<CachedMessageAnalysis[]>(MESSAGE_KEY) ?? [];
  return stored.find((item) => item.itemId === itemId && item.language === language) ?? null;
}

export interface StoredReplyDraft {
  itemId: string;
  tone: DraftTone;
  toneDetails: string;
  instructions: string;
  subject: string;
  body: string;
  savedAt: string | null;
}

export function loadReplyDraft(itemId: string): StoredReplyDraft | null {
  const stored = readJson<StoredReplyDraft[]>(REPLY_KEY) ?? [];
  return stored.find((item) => item.itemId === itemId) ?? null;
}

export function forgetReplyDraft(itemId: string): void {
  const stored = readJson<StoredReplyDraft[]>(REPLY_KEY) ?? [];
  writeJson(REPLY_KEY, stored.filter((item) => item.itemId !== itemId));
}

export function saveReplyDraftLocal(draft: StoredReplyDraft): void {
  const stored = readJson<StoredReplyDraft[]>(REPLY_KEY) ?? [];
  const next = stored.filter((item) => item.itemId !== draft.itemId);
  next.push(draft);
  writeJson(REPLY_KEY, next.slice(-80));
}

export function saveMessageAnalysis(itemId: string, language: string, result: AnalyzeMailResponse): void {
  const stored = readJson<CachedMessageAnalysis[]>(MESSAGE_KEY) ?? [];
  const next = stored.filter((item) => item.itemId !== itemId || item.language !== language);
  next.push({ itemId, language, result });
  writeJson(MESSAGE_KEY, next.slice(-MAX_MESSAGES));
}

function readJson<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) as T : null;
  } catch {
    return null;
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // A full browser store must not block the already shown list.
  }
}
