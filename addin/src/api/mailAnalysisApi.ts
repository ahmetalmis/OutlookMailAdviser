import type {
  AnalyzeMailRequest,
  AnalyzeMailResponse,
  ApiStatus,
  MailMessage,
  MailQuestionResponse,
  DraftMailRequest,
  DraftMailResponse,
} from "../types";
import { providerErrorMessage } from "./providerErrors";
import { tr } from "../i18n/messages";

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

async function fetchMail(url: string, init: RequestInit): Promise<Response> {
  try {
    return await fetch(url, init);
  } catch (error) {
    if (init.signal?.aborted) throw error;
    throw new MailAnalysisApiError(tr.local_unavailable, 0, "local_unavailable");
  }
}

export async function analyzeMail(
  request: AnalyzeMailRequest,
  signal?: AbortSignal,
): Promise<AnalyzeMailResponse> {
  const response = await fetchMail(`${apiBaseUrl}/api/v1/mail/analysis`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      providerErrorMessage(problem.code),
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
  const response = await fetchMail(`${apiBaseUrl}/api/v1/mail/draft`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
    signal,
  });

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      providerErrorMessage(problem.code),
      response.status,
      problem.code,
    );
  }

  return (await response.json()) as DraftMailResponse;
}

export async function getApiStatus(signal?: AbortSignal): Promise<ApiStatus> {
  try {
    const timeout = AbortSignal.timeout(15000);
    const response = await fetch(`${apiBaseUrl}/health/ready`, {
      signal: signal ? AbortSignal.any([signal, timeout]) : timeout,
      cache: "no-store",
    });

    if (response.headers.get("content-type")?.includes("application/json")) {
      const payload = (await response.json()) as Record<string, unknown>;
      const data = payload.data as Record<string, unknown> | undefined;
      const code = typeof payload.code === "string" ? payload.code : undefined;
      return {
        state: response.ok ? "healthy" : "unhealthy",
        provider: typeof data?.provider === "string" ? data.provider : undefined,
        model: typeof data?.model === "string" ? data.model : undefined,
        code: response.ok ? undefined : code ?? "provider_not_ready",
        detail: response.ok ? undefined : providerErrorMessage(code ?? "provider_not_ready"),
      };
    }

    // ASP.NET Core's default health-check writer returns plain text ("Healthy").
    return response.ok ? { state: "healthy" }
      : { state: "unhealthy", code: "provider_not_ready", detail: tr.provider_not_ready };
  } catch (error) {
    return {
      state: "unhealthy",
      code: error instanceof Error && error.name === "TimeoutError" ? "status_timeout" : "local_unavailable",
      detail: error instanceof Error && error.name === "TimeoutError"
        ? tr.status_timeout
        : tr.local_unavailable,
    };
  }
}

export async function askMailQuestion(
  message: MailMessage, question: string, preferredLanguage: string, signal?: AbortSignal,
): Promise<MailQuestionResponse> {
  const response = await fetchMail(`${apiBaseUrl}/api/v1/mail/questions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message, question, preferredLanguage }),
    signal,
  });
  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new MailAnalysisApiError(
      providerErrorMessage(problem.code),
      response.status, problem.code,
    );
  }
  return await response.json() as MailQuestionResponse;
}
