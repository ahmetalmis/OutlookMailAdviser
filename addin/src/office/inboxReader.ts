import type { MailParticipant } from "../types";
import { fetchInbox, fetchMailFolders, fetchMessageBodies, markMailboxItemRead, openMailboxItem, type MailFolder } from "../api/mailAnalysisApi";

export interface InboxCandidate {
  itemId: string;
  conversationId: string | null;
  subject: string;
  from: MailParticipant;
  to: MailParticipant[];
  cc: MailParticipant[];
  receivedAt: string | null;
  preview: string;
  hasUnsubscribeHint: boolean;
  isAutoSubmitted: boolean;
  unread: boolean;
}

interface GraphRecipient {
  emailAddress?: { name?: string; address?: string };
}

interface GraphHeader {
  name?: string;
  value?: string;
}

interface GraphMessage {
  id?: string;
  conversationId?: string;
  subject?: string;
  receivedDateTime?: string;
  bodyPreview?: string;
  from?: GraphRecipient;
  toRecipients?: GraphRecipient[];
  ccRecipients?: GraphRecipient[];
  internetMessageHeaders?: GraphHeader[];
  body?: { content?: string };
  isRead?: boolean;
}

export async function listMailFolders(signal?: AbortSignal): Promise<MailFolder[]> {
  return fetchMailFolders(signal);
}

export async function listInbox(options: { since: Date; unreadOnly: boolean; folderIds?: string[]; signal?: AbortSignal }): Promise<InboxCandidate[]> {
  const messages = await fetchInbox(options.since, options.unreadOnly, options.folderIds ?? [], options.signal);
  return messages.map((message) => ({ ...message, unread: message.unread !== false }));
}

export async function loadMessageBodies(itemIds: string[]): Promise<Map<string, string>> {
  return fetchMessageBodies(itemIds);
}

export async function openInboxMessage(itemId: string): Promise<void> {
  await openMailboxItem(itemId);
}

export async function markInboxMessageRead(itemId: string): Promise<void> {
  await markMailboxItemRead(itemId);
}

export function mapGraphMessages(messages: GraphMessage[]): InboxCandidate[] {
  return messages.flatMap((message) => {
    const from = participant(message.from);
    if (!message.id || !from) {
      return [];
    }

    const headers = message.internetMessageHeaders ?? [];
    return [{
      itemId: message.id,
      conversationId: message.conversationId ?? null,
      subject: message.subject?.trim() || "(Konu yok)",
      from,
      to: (message.toRecipients ?? []).map(participant).filter((item): item is MailParticipant => item !== null),
      cc: (message.ccRecipients ?? []).map(participant).filter((item): item is MailParticipant => item !== null),
      receivedAt: message.receivedDateTime ?? null,
      preview: (message.bodyPreview ?? "").slice(0, 400),
      hasUnsubscribeHint: headers.some((header) => {
        const name = header.name?.toLowerCase() ?? "";
        const value = header.value?.toLowerCase() ?? "";
        return name === "list-unsubscribe" || name === "list-id"
          || (name === "precedence" && (value.includes("bulk") || value.includes("list")));
      }),
      unread: message.isRead !== true,
      isAutoSubmitted: headers.some((header) => {
        const value = header.value?.trim().toLowerCase() ?? "";
        return header.name?.toLowerCase() === "auto-submitted" && value.length > 0 && value !== "no";
      }),
    }];
  });
}

function participant(recipient: GraphRecipient | undefined): MailParticipant | null {
  const address = recipient?.emailAddress?.address?.trim();
  if (!address) {
    return null;
  }

  return {
    name: recipient?.emailAddress?.name?.trim() || null,
    address,
  };
}
