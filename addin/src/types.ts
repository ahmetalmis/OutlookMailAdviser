export interface MailParticipant {
  name: string | null;
  address: string;
}

export interface MailMessage {
  itemId: string | null;
  subject: string;
  from: MailParticipant;
  to: MailParticipant[];
  cc: MailParticipant[];
  sentAt: string | null;
  body: string;
  bodyFormat: "html" | "plainText";
}

export interface AnalyzeMailRequest {
  clientRequestId: string;
  preferredLanguage: string;
  message: MailMessage;
  currentUser?: MailParticipant;
}

export interface MailAction {
  description: string;
  owner: string | null;
  dueDate: string | null;
  confidence: number;
  assignedToCurrentUser: boolean;
}

export interface AnalyzeMailResponse {
  clientRequestId: string;
  summary: string;
  actionRequired: boolean;
  actionRequiredFromCurrentUser: boolean;
  actions: MailAction[];
  priority: string;
  sentiment: string;
  warnings: string[];
  model: string;
  durationMilliseconds: number;
  wasTruncated: boolean;
}

export type DraftTone = "professional" | "friendly" | "concise" | "persuasive" | "empathetic";

export interface DraftMailRequest {
  clientRequestId: string;
  preferredLanguage: string;
  message: MailMessage;
  tone: DraftTone;
  toneDetails?: string;
  instructions: string;
}

export interface DraftMailResponse {
  clientRequestId: string;
  subject: string;
  body: string;
  tone: DraftTone;
  model: string;
  durationMilliseconds: number;
  wasTruncated: boolean;
}

export interface ApiStatus {
  state: "healthy" | "unhealthy";
  provider?: string;
  model?: string;
  detail?: string;
}
