import { useEffect, useState } from "react";
import { draftMail, MailAnalysisApiError, saveReplyDraft, sendReply } from "../api/mailAnalysisApi";
import { forgetReplyDraft, loadReplyDraft, saveReplyDraftLocal } from "../office/analysisCache";
import { readCurrentUser, type OutputLanguage } from "../office/outlookItemReader";
import { draftToneOptions, type DraftTone, type MailMessage, type MailParticipant } from "../types";

interface ReplyAudience {
  from: MailParticipant;
  to: MailParticipant[];
  cc: MailParticipant[];
}

interface Props {
  itemId: string;
  outputLanguage: OutputLanguage;
  audience: ReplyAudience;
  loadMessage: () => Promise<MailMessage | null>;
  onSent?: (itemId: string) => void;
}

const emptyDraft = {
  tone: "professional" as DraftTone,
  toneDetails: "",
  instructions: "",
  subject: "",
  body: "",
  savedAt: null as string | null,
};

export function ReplyDraftPanel({ itemId, outputLanguage, audience, loadMessage, onSent }: Props) {
  const [form, setForm] = useState(() => {
    const stored = loadReplyDraft(itemId);
    const tone = stored && draftToneOptions.some((option) => option.value === stored.tone)
      ? stored.tone
      : "professional";
    return stored ? { ...stored, tone, toneDetails: "" } : { ...emptyDraft, itemId };
  });
  const [drafting, setDrafting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [sending, setSending] = useState(false);
  const [confirmMode, setConfirmMode] = useState<"sender" | "all" | null>(null);
  const [recipients, setRecipients] = useState<DraftRecipient[] | null>(null);
  const [addAddress, setAddAddress] = useState("");
  const [addKind, setAddKind] = useState<"to" | "cc">("to");
  const [copied, setCopied] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!form.instructions && !form.toneDetails && !form.subject && !form.body) {
      return;
    }

    saveReplyDraftLocal({
      itemId,
      tone: form.tone,
      toneDetails: form.toneDetails,
      instructions: form.instructions,
      subject: form.subject,
      body: form.body,
      savedAt: form.savedAt,
    });
  }, [form, itemId]);

  async function handleCreate() {
    const instructions = form.instructions.trim();
    if (!instructions) {
      setError("Hazırlanacak yanıtın kısa bir özetini veya talimatını yazın.");
      return;
    }

    setDrafting(true);
    setError(null);
    setNotice(null);
    try {
      const message = await loadMessage();
      if (!message) {
        setError("Mail gövdesi okunamadı. Outlook'un açık olduğunu kontrol edin.");
        return;
      }

      const draft = await draftMail({
        clientRequestId: crypto.randomUUID(),
        preferredLanguage: outputLanguage,
        tone: form.tone,
        instructions,
        message,
      });
      setForm((current) => ({
        ...current,
        subject: draft.subject,
        body: draft.body,
        savedAt: null,
      }));
    } catch (draftError) {
      setError(errorMessage(draftError));
    } finally {
      setDrafting(false);
    }
  }

  async function handleSave() {
    const subject = form.subject.trim();
    const body = form.body.trim();
    if (!subject || !body) {
      setError("Kaydetmek için konu ve yanıt metni gerekli.");
      return;
    }

    setSaving(true);
    setError(null);
    setNotice(null);
    try {
      await saveReplyDraft(itemId, subject, body);
      const savedAt = new Date().toISOString();
      setForm((current) => ({ ...current, savedAt }));
      setNotice("Outlook Taslaklar klasörüne kaydedildi. Posta gönderilmedi. Klasörü görmüyorsanız bir kez kapatıp açın.");
    } catch (saveError) {
      setError(errorMessage(saveError));
    } finally {
      setSaving(false);
    }
  }

  async function handleSend(replyAll: boolean) {
    const subject = form.subject.trim();
    const body = form.body.trim();
    if (!subject || !body) {
      setError("Göndermek için konu ve yanıt metni gerekli.");
      return;
    }

    const mode = replyAll ? "all" : "sender";
    const chosen = replyAll ? (recipients ?? seedRecipients(audience)) : null;
    if (confirmMode !== mode) {
      if (chosen) {
        setRecipients(chosen);
      }
      setConfirmMode(mode);
      setError(null);
      setNotice(replyAll
        ? "Alıcıları düzenleyin. Adresin @ sonrası kuruluşu gösterir. Göndermek için tekrar Tümünü cevapla'ya basın."
        : "Yalnızca gönderene gidecek. Göndermek için tekrar Cevapla'ya basın.");
      return;
    }

    if (chosen && chosen.length === 0) {
      setError("Göndermek için en az bir alıcı bırakın.");
      return;
    }

    setSending(true);
    setError(null);
    setNotice(null);
    try {
      await sendReply(
        itemId,
        subject,
        body,
        replyAll,
        chosen ? { to: peopleOf(chosen, "to"), cc: peopleOf(chosen, "cc") } : undefined,
      );
      setConfirmMode(null);
      onSent?.(itemId);
      setNotice(replyAll
        ? "Yanıt seçtiğiniz kişilere gönderildi. Listeden kaldırıldı."
        : "Yanıt yalnızca gönderene iletildi. Listeden kaldırıldı.");
    } catch (sendError) {
      setConfirmMode(mode);
      setError(errorMessage(sendError));
    } finally {
      setSending(false);
    }
  }

  function addRecipient() {
    const parsed = parseAddress(addAddress);
    if (!parsed) {
      setError("E-posta adresi geçersiz. Örnek: ad.soyad@kurulus.com");
      return;
    }

    const list = recipients ?? seedRecipients(audience);
    if (list.some((person) => sameAddress(person.address, parsed.address))) {
      setError("Bu adres zaten listede.");
      return;
    }

    setRecipients([...list, { key: parsed.address.toLowerCase(), kind: addKind, ...parsed }]);
    setAddAddress("");
    setError(null);
  }

  function handleDeleteDraft() {
    forgetReplyDraft(itemId);
    setConfirmMode(null);
    setCopied(false);
    setError(null);
    setRecipients(null);
    setAddAddress("");
    setForm((current) => ({ ...current, subject: "", body: "", savedAt: null }));
    setNotice("Taslak silindi. Outlook Taslaklar klasöründeki kayıt durur.");
  }

  async function handleCopy() {
    if (!form.body.trim()) {
      return;
    }

    try {
      await navigator.clipboard.writeText(`Konu: ${form.subject}\n\n${form.body}`);
      setCopied(true);
    } catch {
      setError("Taslak panoya kopyalanamadı.");
    }
  }

  return (
    <div className="priority-draft">
      <span className="eyebrow">Yanıt taslağı</span>
      <label className="draft-field">
        <span>Ton</span>
        <select
          value={form.tone}
          disabled={drafting || saving || sending}
          onChange={(event) => setForm((current) => ({ ...current, tone: event.target.value as DraftTone }))}
        >
          {draftToneOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>
      <label className="draft-field">
        <span>Açıklama / yanıt özeti</span>
        <textarea
          rows={4}
          maxLength={2000}
          value={form.instructions}
          disabled={drafting || saving || sending}
          placeholder="Örn. Bugün öğleden sonra uygun olduğumu yaz."
          onChange={(event) => setForm((current) => ({ ...current, instructions: event.target.value }))}
        />
      </label>
      <button
        type="button"
        className="draft-button"
        disabled={drafting || saving || sending || !form.instructions.trim()}
        onClick={() => void handleCreate()}
      >
        {drafting ? "Taslak hazırlanıyor…" : "Yanıt taslağı oluştur"}
      </button>
      {(form.subject || form.body) && (
        <div className="draft-result">
          <label className="draft-field">
            <span>Konu</span>
            <input
              type="text"
              value={form.subject}
              maxLength={255}
              disabled={saving || sending}
              onChange={(event) => {
                setConfirmMode(null);
                setForm((current) => ({ ...current, subject: event.target.value, savedAt: null }));
              }}
            />
          </label>
          <label className="draft-field">
            <span>Yanıt</span>
            <textarea
              rows={6}
              value={form.body}
              disabled={saving || sending}
              onChange={(event) => {
                setConfirmMode(null);
                setForm((current) => ({ ...current, body: event.target.value, savedAt: null }));
              }}
            />
          </label>
          {confirmMode === "all" ? (
            <ReplyAudienceEditor
              people={recipients ?? seedRecipients(audience)}
              addAddress={addAddress}
              addKind={addKind}
              disabled={sending}
              onAddAddress={setAddAddress}
              onAddKind={setAddKind}
              onAdd={addRecipient}
              onKind={(key, kind) => setRecipients((current) => (current ?? seedRecipients(audience)).map((person) => person.key === key ? { ...person, kind } : person))}
              onRemove={(key) => setRecipients((current) => (current ?? seedRecipients(audience)).filter((person) => person.key !== key))}
            />
          ) : confirmMode === "sender" ? (
            <div className="reply-audience">
              <strong>Yalnızca gönderen</strong>
              <PersonList people={[audience.from]} />
            </div>
          ) : (
            <p className="reply-audience-hint">
              Kime: {peopleHint(audience.to)} · Bilgi: {peopleHint(audience.cc)}
            </p>
          )}
          <div className="priority-draft-actions">
            <button
              type="button"
              className="draft-button"
              disabled={sending || saving || drafting || !form.subject.trim() || !form.body.trim()}
              onClick={() => void handleSend(false)}
            >
              {sending && confirmMode === null ? "Gönderiliyor…" : confirmMode === "sender" ? "Cevapla — gönder" : "Cevapla"}
            </button>
            <button
              type="button"
              className="draft-button"
              disabled={sending || saving || drafting || !form.subject.trim() || !form.body.trim()}
              onClick={() => void handleSend(true)}
            >
              {confirmMode === "all" ? "Tümünü cevapla — gönder" : "Tümünü cevapla"}
            </button>
            {confirmMode && (
              <button type="button" disabled={sending} onClick={() => { setConfirmMode(null); setNotice(null); }}>
                Vazgeç
              </button>
            )}
            <button
              type="button"
              className="draft-button"
              disabled={saving || sending || drafting || !form.subject.trim() || !form.body.trim()}
              onClick={() => void handleSave()}
            >
              {saving ? "Kaydediliyor…" : "Kaydet"}
            </button>
            <button type="button" disabled={sending} onClick={() => void handleCopy()}>{copied ? "Kopyalandı" : "Kopyala"}</button>
            <button type="button" disabled={sending || saving || drafting} onClick={handleDeleteDraft}>Sil</button>
          </div>
          <p className="priority-status">Cevapla yalnızca gönderene gider. Tümünü cevapla’da kişileri ekleyip çıkarabilirsiniz. @ sonrası kuruluştur. Sil, ekrandaki taslağı temizler.</p>
          {form.savedAt && !notice && <p className="priority-status">Bu metin Outlook Taslaklar klasörüne kaydedildi.</p>}
        </div>
      )}
      {notice && <p className="priority-status">{notice}</p>}
      {error && <div className="error" role="alert">{error}</div>}
    </div>
  );
}

interface DraftRecipient extends MailParticipant {
  key: string;
  kind: "to" | "cc";
}

function ReplyAudienceEditor({
  people,
  addAddress,
  addKind,
  disabled,
  onAddAddress,
  onAddKind,
  onAdd,
  onKind,
  onRemove,
}: {
  people: DraftRecipient[];
  addAddress: string;
  addKind: "to" | "cc";
  disabled: boolean;
  onAddAddress: (value: string) => void;
  onAddKind: (kind: "to" | "cc") => void;
  onAdd: () => void;
  onKind: (key: string, kind: "to" | "cc") => void;
  onRemove: (key: string) => void;
}) {
  return (
    <div className="reply-audience" role="group" aria-label="Alıcılar">
      <strong>Gidecek kişiler</strong>
      <p>Siz baştan çıkarıldı. Çıkar ile kişiyi silin, alttan yeni adres ekleyin. Koyu yazılan bölüm kuruluştur.</p>
      {people.length === 0 && <em className="reply-empty">Alıcı kalmadı. En az bir adres ekleyin.</em>}
      <div className="reply-people">
        {people.map((person) => (
          <span className="reply-person" key={person.key}>
            <select
              aria-label={`${displayName(person)} kime veya bilgi`}
              value={person.kind}
              disabled={disabled}
              onChange={(event) => onKind(person.key, event.target.value as "to" | "cc")}
            >
              <option value="to">Kime</option>
              <option value="cc">Bilgi</option>
            </select>
            <span className="reply-person-text">
              {displayName(person) && <b>{displayName(person)}</b>}
              <AddressLine address={person.address} />
            </span>
            <button type="button" className="reply-remove" disabled={disabled} onClick={() => onRemove(person.key)}>Çıkar</button>
          </span>
        ))}
      </div>
      <div className="reply-add">
        <input
          type="email"
          value={addAddress}
          disabled={disabled}
          placeholder="ad.soyad@kurulus.com"
          aria-label="Eklenecek e-posta"
          onChange={(event) => onAddAddress(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              onAdd();
            }
          }}
        />
        <select aria-label="Eklenecek alan" value={addKind} disabled={disabled} onChange={(event) => onAddKind(event.target.value as "to" | "cc")}>
          <option value="to">Kime</option>
          <option value="cc">Bilgi</option>
        </select>
        <button type="button" className="reply-add-button" disabled={disabled || !addAddress.trim()} onClick={onAdd}>Ekle</button>
      </div>
    </div>
  );
}

function PersonList({ people }: { people: MailParticipant[] }) {
  if (people.length === 0) {
    return <em className="reply-empty">Yok</em>;
  }

  return (
    <div className="reply-people">
      {people.map((person) => (
        <span className="reply-person" key={person.address}>
          <span className="reply-person-text">
            {displayName(person) && <b>{displayName(person)}</b>}
            <AddressLine address={person.address} />
          </span>
        </span>
      ))}
    </div>
  );
}

function AddressLine({ address }: { address: string }) {
  const at = address.lastIndexOf("@");
  if (at < 1 || at === address.length - 1) {
    return <small>{address}</small>;
  }

  return (
    <small>
      {address.slice(0, at + 1)}
      <b>{address.slice(at + 1)}</b>
    </small>
  );
}

function displayName(person: MailParticipant): string {
  const name = person.name?.trim() ?? "";
  return name && name.toLowerCase() !== person.address.trim().toLowerCase() ? name : "";
}

function peopleHint(people: MailParticipant[]): string {
  if (people.length === 0) {
    return "yok";
  }

  const names = people.map((person) => {
    const domain = person.address.split("@")[1];
    const name = person.name?.trim();
    return name && domain ? `${name} · ${domain}` : person.address;
  });
  return names.length <= 3 ? names.join(", ") : `${names.slice(0, 2).join(", ")} +${names.length - 2}`;
}

function seedRecipients(audience: ReplyAudience): DraftRecipient[] {
  const self = readCurrentUser()?.address;
  const rows: DraftRecipient[] = [];
  const seen = new Set<string>();
  const push = (kind: "to" | "cc", person: MailParticipant) => {
    const address = person.address.trim();
    const key = address.toLowerCase();
    if (!address.includes("@") || sameAddress(address, self) || seen.has(key)) {
      return;
    }

    seen.add(key);
    rows.push({ key, kind, name: person.name, address });
  };

  push("to", audience.from);
  audience.to.forEach((person) => push("to", person));
  audience.cc.forEach((person) => push("cc", person));
  return rows;
}

function peopleOf(people: DraftRecipient[], kind: "to" | "cc"): MailParticipant[] {
  return people.filter((person) => person.kind === kind).map(({ name, address }) => ({ name, address }));
}

function parseAddress(value: string): MailParticipant | null {
  const trimmed = value.trim();
  const wrapped = trimmed.match(/^(.*)<([^>]+)>$/);
  const address = (wrapped?.[2] ?? trimmed).trim();
  const name = wrapped?.[1]?.replace(/["']/g, "").trim() || null;
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) {
    return null;
  }

  return { name: name || null, address };
}

function sameAddress(left: string, right: string | undefined): boolean {
  return Boolean(right) && left.trim().toLowerCase() === right.trim().toLowerCase();
}

function errorMessage(error: unknown): string {
  if (error instanceof MailAnalysisApiError) {
    return error.code ? `${error.message} (${error.code})` : error.message;
  }

  return error instanceof Error ? error.message : "Taslak tamamlanamadı.";
}
