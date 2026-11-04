import type { AnalyzeMailRequest, MailMessage, MailParticipant } from "../types";

function participant(details: Office.EmailAddressDetails): MailParticipant {
  return {
    name: details.displayName?.trim() || null,
    address: details.emailAddress,
  };
}

function readBody(item: Office.MessageRead): Promise<string> {
  return new Promise((resolve, reject) => {
    item.body.getAsync(Office.CoercionType.Html, (result) => {
      if (result.status === Office.AsyncResultStatus.Succeeded) {
        resolve(result.value);
        return;
      }

      reject(new Error(result.error?.message ?? "E-posta gövdesi okunamadı."));
    });
  });
}

function isMessageRead(item: Office.Item | undefined): item is Office.MessageRead {
  return Boolean(item && "from" in item && "body" in item && "to" in item);
}

export async function readCurrentMessage(): Promise<MailMessage> {
  const item = Office.context.mailbox.item;
  if (!isMessageRead(item)) {
    throw new Error("Lütfen okuma modunda bir e-posta açın.");
  }

  if (!item.from?.emailAddress) {
    throw new Error("Gönderen bilgisi okunamadı.");
  }

  const recipients = item.to ?? [];
  if (recipients.length === 0) {
    throw new Error("Alıcı bilgisi okunamadı.");
  }

  return {
    itemId: item.itemId ?? null,
    subject: item.subject ?? "(Konu yok)",
    from: participant(item.from),
    to: recipients.map(participant),
    cc: (item.cc ?? []).map(participant),
    sentAt: item.dateTimeCreated?.toISOString() ?? null,
    body: await readBody(item),
    bodyFormat: "html",
  };
}

export type OutputLanguage = "tr" | "en";

export function readCurrentUser(): MailParticipant | undefined {
  const profile = Office.context.mailbox.userProfile;
  const address = profile?.emailAddress?.trim();
  if (!address) {
    return undefined;
  }

  return {
    name: profile.displayName?.trim() || null,
    address,
  };
}

export async function createAnalysisRequest(
  preferredLanguage: OutputLanguage = "tr",
): Promise<AnalyzeMailRequest> {
  return {
    clientRequestId: crypto.randomUUID(),
    preferredLanguage,
    message: await readCurrentMessage(),
    currentUser: readCurrentUser(),
  };
}

export function registerItemChanged(handler: () => void): void {
  Office.context.mailbox.addHandlerAsync(Office.EventType.ItemChanged, handler);
}
