import type {
  AnalyzeMailRequest,
  AnalyzeMailResponse,
  ApiStatus,
  DraftMailRequest,
  DraftMailResponse,
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
