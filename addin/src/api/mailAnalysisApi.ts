import type {
  AnalyzeMailRequest,
  AnalyzeMailResponse,
  ApiStatus,
  DraftMailRequest,
  DraftMailResponse,
  MailParticipant,
  MailboxTriageRequest,
  MailboxTriageResponse,
} from "../types";

const defaultBaseUrl = import.meta.env.DEV
  ? "https://localhost:7047"
  : window.location.origin;
const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL ?? defaultBaseUrl;
const apiBaseUrl = configuredBaseUrl.replace(/\/$/, "");

interface ProblemDetails {
  title?: string;
  detail?: string;
  code?: string;
}

export class MailAnalysisApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string,
  ) {
    super(message);
    this.name = "MailAnalysisApiError";
  }
}

async function parseProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return {};
  }
}

export async function analyzeMail(
  request: AnalyzeMailRequest,
  signal?: AbortSignal,
): Promise<AnalyzeMailResponse> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mail/analysis`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `API isteği başarısız oldu (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as AnalyzeMailResponse;
}

export async function draftMail(
  request: DraftMailRequest,
  signal?: AbortSignal,
): Promise<DraftMailResponse> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mail/draft`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Taslak isteği başarısız oldu (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as DraftMailResponse;
}

export async function triageMailbox(
  request: MailboxTriageRequest,
  signal?: AbortSignal,
): Promise<MailboxTriageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/triage`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Kutu taraması başarısız oldu (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as MailboxTriageResponse;
}

export interface MailFolder {
  entryId: string;
  name: string;
  path: string;
  kind: string;
}

export async function fetchMailFolders(signal?: AbortSignal): Promise<MailFolder[]> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/folders`, { signal });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Klasörler okunamadı (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as MailFolder[];
}

export async function fetchInbox(
  since: Date,
  unreadOnly: boolean,
  folderIds: string[] = [],
  signal?: AbortSignal,
): Promise<import("../office/inboxReader").InboxCandidate[]> {
  const params = new URLSearchParams({
    since: since.toISOString(),
    unreadOnly: String(unreadOnly),
  });
  folderIds.slice(0, 20).forEach((folderId) => params.append("folder", folderId));
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/inbox?${params}`, { signal });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Gelen kutusu okunamadı (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as import("../office/inboxReader").InboxCandidate[];
}

export async function fetchMessageBodies(itemIds: string[], signal?: AbortSignal): Promise<Map<string, string>> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/bodies`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ itemIds }),
    signal,
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Mail gövdesi okunamadı (${response.status}).`,
      response.status,
      problem.code,
    );
  }

  const payload = (await response.json()) as Array<{ itemId: string; body: string }>;
  return new Map(payload.map((item) => [item.itemId, item.body]));
}

export async function sendReply(
  itemId: string,
  subject: string,
  body: string,
  replyAll = false,
  recipients?: { to: MailParticipant[]; cc: MailParticipant[] },
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/reply-send`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      itemId,
      subject,
      body,
      replyAll,
      to: replyAll ? recipients?.to ?? [] : undefined,
      cc: replyAll ? recipients?.cc ?? [] : undefined,
    }),
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Yanıt gönderilemedi (${response.status}).`,
      response.status,
      problem.code,
    );
  }
}

export async function saveReplyDraft(itemId: string, subject: string, body: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/reply-draft`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ itemId, subject, body }),
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Taslak kaydedilemedi (${response.status}).`,
      response.status,
      problem.code,
    );
  }
}

export async function markMailboxItemRead(itemId: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/mark-read`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ itemId }),
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Mail okundu işaretlenemedi (${response.status}).`,
      response.status,
      problem.code,
    );
  }
}

export async function openMailboxItem(itemId: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/v1/mailbox/open`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ itemId }),
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      problem.detail ?? problem.title ?? `Mail açılamadı (${response.status}).`,
      response.status,
      problem.code,
    );
  }
}

export async function getApiStatus(signal?: AbortSignal): Promise<ApiStatus> {
  try {
    const response = await fetch(`${apiBaseUrl}/health/ready`, { signal });
    if (!response.ok) {
      return { state: "unhealthy", detail: `HTTP ${response.status}` };
    }

    if (response.headers.get("content-type")?.includes("application/json")) {
      const payload = (await response.json()) as Record<string, unknown>;
      const data = payload.data as Record<string, unknown> | undefined;
      return {
        state: "healthy",
        provider: typeof data?.provider === "string" ? data.provider : undefined,
        model: typeof data?.model === "string" ? data.model : undefined,
      };
    }

    // ASP.NET Core's default health-check writer returns plain text ("Healthy").
    return { state: "healthy" };
  } catch (error) {
    return {
      state: "unhealthy",
      detail: error instanceof Error ? error.message : "API'ye ulaşılamadı.",
    };
  }
}
