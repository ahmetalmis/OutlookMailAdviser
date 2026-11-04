import { useEffect, useRef, useState } from "react";
import { analyzeMail, MailAnalysisApiError, triageMailbox } from "../api/mailAnalysisApi";
import { AnalysisResult } from "./AnalysisResult";
import { ReplyDraftPanel } from "./ReplyDraftPanel";
import { loadMessageAnalysis, loadPriorityCache, loadRepliedIds, rememberReplied, saveMessageAnalysis, savePriorityCache, type CachedPriorityMail } from "../office/analysisCache";
import { listInbox, listMailFolders, loadMessageBodies, markInboxMessageRead, openInboxMessage, type InboxCandidate } from "../office/inboxReader";
import type { MailFolder } from "../api/mailAnalysisApi";
import { readCurrentUser, type OutputLanguage } from "../office/outlookItemReader";
import type { AnalyzeMailResponse, MailboxTriageItem, MailMessage } from "../types";

interface Props {
  outputLanguage: OutputLanguage;
}

interface PriorityCard {
  item: MailboxTriageItem;
  source?: InboxCandidate;
  summary: string;
  priority: string;
  personal: boolean;
  verifying: boolean;
  read: boolean;
  body?: string;
}

interface MailDetail {
  result: AnalyzeMailResponse;
  message: MailMessage;
}

const priorityLabel: Record<string, string> = {
  urgent: "Yüksek",
  high: "Yüksek",
  normal: "Orta",
  low: "Düşük",
};

const priorityClass: Record<string, string> = {
  urgent: "high",
  high: "high",
  normal: "medium",
  low: "low",
};

const RANGE_KEY = "mail-adviser.inbox-range";
const UNREAD_KEY = "mail-adviser.inbox-unread-only";
const FOLDER_KEY = "mail-adviser.inbox-folders";

const inboxRanges = [
  { id: "8h", label: "Son 8 saat", hours: 8 },
  { id: "24h", label: "Son 24 saat", hours: 24 },
  { id: "48h", label: "Son 48 saat", hours: 48 },
  { id: "3d", label: "Son 3 gün", hours: 72 },
  { id: "7d", label: "Son 7 gün", hours: 168 },
  { id: "30d", label: "Son 30 gün", hours: 720 },
] as const;

type InboxRangeId = (typeof inboxRanges)[number]["id"];

function storedRange(): InboxRangeId {
  const stored = localStorage.getItem(RANGE_KEY);
  return inboxRanges.some((range) => range.id === stored) ? stored as InboxRangeId : "24h";
}

function rangeById(id: InboxRangeId) {
  return inboxRanges.find((range) => range.id === id) ?? inboxRanges[1];
}

function folderSummary(selected: string[], available: MailFolder[]): string {
  if (selected.length === 0) {
    return "Gelen Kutusu";
  }

  const names = available.filter((folder) => selected.includes(folder.entryId)).map((folder) => folder.name);
  if (names.length === 0) {
    return `${selected.length} klasör`;
  }

  return names.length <= 2 ? names.join(", ") : `${names.length} klasör`;
}

function isFolderSelected(folder: MailFolder, selected: string[], available: MailFolder[]): boolean {
  if (selected.length === 0) {
    return folder.kind === "inbox";
  }

  return selected.includes(folder.entryId) || (available.length === 0 && folder.kind === "inbox");
}

function storedFolderIds(): string[] {
  try {
    const parsed = JSON.parse(localStorage.getItem(FOLDER_KEY) ?? "[]") as unknown;
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string" && item.length > 0).slice(0, 20) : [];
  } catch {
    return [];
  }
}

export function PriorityInbox({ outputLanguage }: Props) {
  const [searched, setSearched] = useState(false);
  const [scannedCount, setScannedCount] = useState(0);
  const [actionCards, setActionCards] = useState<PriorityCard[]>([]);
  const [reviewItems, setReviewItems] = useState<CachedPriorityMail[]>([]);
  const [statusText, setStatusText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [markingId, setMarkingId] = useState<string | null>(null);
  const [details, setDetails] = useState<Record<string, MailDetail>>({});
  const detailsRef = useRef(details);
  detailsRef.current = details;
  const [rangeId, setRangeId] = useState<InboxRangeId>(storedRange);
  const [unreadOnly, setUnreadOnly] = useState(() => localStorage.getItem(UNREAD_KEY) !== "false");
  const [folderIds, setFolderIds] = useState<string[]>(storedFolderIds);
  const [folders, setFolders] = useState<MailFolder[]>([]);
  const [foldersOpen, setFoldersOpen] = useState(false);
  const [foldersLoading, setFoldersLoading] = useState(false);

  useEffect(() => {
    const since = new Date(Date.now() - rangeById(rangeId).hours * 60 * 60 * 1000);
    const replied = new Set(loadRepliedIds());
    const cached = loadPriorityCache().filter((item) => (
      item.language === outputLanguage && !replied.has(item.itemId) && inRange(item, since, new Set())
    ));
    setActionCards(cached.filter((item) => item.bucket === "action").map(cardFromCache));
    setReviewItems(cached.filter((item) => item.bucket === "review"));
  }, [outputLanguage, rangeId]);

  useEffect(() => {
    if (!expandedId || detailsRef.current[expandedId]) {
      return;
    }

    const review = reviewItems.find((entry) => entry.itemId === expandedId);
    const card = actionCards.find((entry) => entry.item.itemId === expandedId)
      ?? (review ? cardFromCache(review) : null);
    if (!card) {
      return;
    }

    let cancel = false;
    void hydrateReviews([card], outputLanguage, (itemId, detail) => {
      if (!cancel) {
        setDetails((current) => current[itemId] ? current : { ...current, [itemId]: detail });
      }
    });
    return () => {
      cancel = true;
    };
  }, [expandedId, reviewItems, actionCards, outputLanguage]);

  async function handleAnalyze() {
    setLoading(true);
    setError(null);
    const started = Date.now();
    const controller = new AbortController();
    const abortTimer = window.setTimeout(() => controller.abort(), 90_000);
    const tick = window.setInterval(() => {
      const seconds = Math.round((Date.now() - started) / 1000);
      setStatusText(`Seçili klasörler okunuyor… ${seconds} sn. İzin penceresi çıktıysa İzin Ver'e basın.`);
    }, 1000);
    try {
      const range = rangeById(rangeId);
      setStatusText(`Seçili klasörler okunuyor (${range.label.toLocaleLowerCase("tr-TR")}, ${folderSummary(folderIds, folders)})…`);
      const since = new Date(Date.now() - range.hours * 60 * 60 * 1000);
      const messages = await listInbox({ since, unreadOnly, folderIds, signal: controller.signal });
      window.clearInterval(tick);
      window.clearTimeout(abortTimer);
      setSearched(true);
      setScannedCount(messages.length);
      const stored = loadPriorityCache();
      const known = new Map(
        stored.filter((item) => item.language === outputLanguage).map((item) => [item.itemId, item]),
      );
      const replied = new Set(loadRepliedIds());
      const fresh = messages.filter((message) => !known.has(message.itemId) && !replied.has(message.itemId));
      const seenIds = new Set(messages.map((message) => message.itemId));
      const retained = [...known.values()]
        .map((item) => withReadFlag(item, messages, since, unreadOnly));
      let analyzed = { actions: [] as CachedPriorityMail[], reviews: [] as CachedPriorityMail[] };
      if (fresh.length > 0) {
        setStatusText(`${fresh.length} yeni mail analiz ediliyor. Kayıtlı olanlar atlanıyor…`);
        const byId = new Map(fresh.map((message) => [message.itemId, message]));
        analyzed = await analyzeFresh(fresh, byId);
      } else {
        setStatusText("Yeni mail yok. Kayıtlı analiz gösteriliyor.");
      }

      const merged = [
        ...retained.filter((item) => !fresh.some((message) => message.itemId === item.itemId)),
        ...analyzed.actions,
        ...analyzed.reviews,
      ];
      const visible = merged.filter((item) => !replied.has(item.itemId) && inRange(item, since, seenIds, unreadOnly));
      const others = stored.filter((item) => item.language !== outputLanguage);
      savePriorityCache([...others, ...merged]);
      setActionCards(visible.filter((item) => item.bucket === "action").map(cardFromCache));
      setReviewItems(visible.filter((item) => item.bucket === "review"));
      if (fresh.length > 0) {
        setStatusText(`${analyzed.actions.length + analyzed.reviews.length} yeni analiz kaydedildi.`);
      }
    } catch (refreshError) {
      setStatusText(null);
      setError(controller.signal.aborted
        ? "Outlook 90 saniyede yanıt vermedi. İzin penceresi açıksa İzin Ver'e basın, tepsi menüsünden API'yi durdurup başlatın, sonra tekrar Analiz et."
        : errorMessage(refreshError));
    } finally {
      window.clearInterval(tick);
      window.clearTimeout(abortTimer);
      setLoading(false);
    }
  }

  async function analyzeFresh(
    fresh: InboxCandidate[],
    byId: Map<string, InboxCandidate>,
  ): Promise<{ actions: CachedPriorityMail[]; reviews: CachedPriorityMail[] }> {
    const triage = await triageMailbox({
      clientRequestId: crypto.randomUUID(),
      preferredLanguage: outputLanguage,
      currentUser: readCurrentUser(),
      messages: fresh,
    });
    const reviews = triage.reviewItems.map((item) => cachedFromTriage(item, "review", byId.get(item.itemId), outputLanguage));
    const actions: CachedPriorityMail[] = [];
    const targets = [...triage.actionItems, ...triage.reviewItems];
    const bodies = await loadBodiesChunked(targets.map((item) => item.itemId));
    const currentUser = readCurrentUser();
    await mapPool(targets, 3, async (item) => {
      const source = byId.get(item.itemId);
      const body = bodies.get(item.itemId);
      const to = source && source.to.length > 0 ? source.to : currentUser ? [currentUser] : [];
      if (!source || !body || to.length === 0) {
        return;
      }

      try {
        const message: MailMessage = {
          itemId: source.itemId,
          subject: source.subject,
          from: source.from,
          to,
          cc: source.cc,
          sentAt: source.receivedAt,
          body,
          bodyFormat: "html",
        };
        const analysis = await analyzeMail({
          clientRequestId: crypto.randomUUID(),
          preferredLanguage: outputLanguage,
          message,
          currentUser,
        });
        saveMessageAnalysis(source.itemId, outputLanguage, analysis);
        setDetails((current) => ({ ...current, [source.itemId]: { result: analysis, message } }));
        const wasAction = triage.actionItems.some((action) => action.itemId === item.itemId);
        const onActionList = wasAction || analysis.actionRequiredFromCurrentUser;
        const cached = cachedFromTriage(
          { ...item, reason: analysis.summary, priority: analysis.priority },
          onActionList ? "action" : "review",
          source,
          outputLanguage,
        );
        if (onActionList) {
          actions.push(cached);
          const index = reviews.findIndex((review) => review.itemId === source.itemId);
          if (index >= 0) {
            reviews.splice(index, 1);
          }
        } else if (!reviews.some((review) => review.itemId === source.itemId)) {
          reviews.push(cached);
        } else {
          const index = reviews.findIndex((review) => review.itemId === source.itemId);
          reviews[index] = cached;
        }
      } catch {
        // A failed call stays uncached so the next Analiz et can retry it.
      }
    });
    return { actions, reviews };
  }

  function removeReplied(itemId: string) {
    rememberReplied(itemId);
    setStatusText("Yanıt gönderildi. Mail listeden kaldırıldı.");
    setActionCards((current) => current.filter((card) => card.item.itemId !== itemId));
    setReviewItems((current) => current.filter((item) => item.itemId !== itemId));
    setDetails((current) => {
      const next = { ...current };
      delete next[itemId];
      return next;
    });
  }

  function dismissFromList(itemId: string) {
    savePriorityCache(loadPriorityCache().filter((item) => item.itemId !== itemId));
    setStatusText("Listeden kaldırıldı. Yeniden Analiz et derseniz, kritere uyuyorsa tekrar gelir.");
    setActionCards((current) => current.filter((card) => card.item.itemId !== itemId));
    setReviewItems((current) => current.filter((item) => item.itemId !== itemId));
    setDetails((current) => {
      const next = { ...current };
      delete next[itemId];
      return next;
    });
  }

  async function markReadInMailbox(itemId: string) {
    setMarkingId(itemId);
    setError(null);
    try {
      await markInboxMessageRead(itemId);
      setStatusText("Okundu işaretlendi ve listeden kaldırıldı.");
    } catch (markError) {
      setStatusText("Listeden kaldırıldı.");
      setError(errorMessage(markError));
    } finally {
      savePriorityCache(loadPriorityCache().filter((item) => item.itemId !== itemId));
      setActionCards((current) => current.filter((card) => card.item.itemId !== itemId));
      setReviewItems((current) => current.filter((item) => item.itemId !== itemId));
      setDetails((current) => {
        const next = { ...current };
        delete next[itemId];
        return next;
      });
      setExpandedId((current) => current === itemId ? null : current);
      setMarkingId(null);
    }
  }

  function markRead(itemId: string) {
    setActionCards((current) => current.map((card) => (
      card.item.itemId === itemId ? { ...card, read: true } : card
    )));
    setReviewItems((current) => current.map((item) => (
      item.itemId === itemId ? { ...item, read: true } : item
    )));
    const stored = loadPriorityCache().map((item) => (
      item.itemId === itemId && item.language === outputLanguage ? { ...item, read: true } : item
    ));
    savePriorityCache(stored);
  }

  async function buildDraftMessage(card: PriorityCard): Promise<MailMessage | null> {
    if (!card.source) {
      return null;
    }

    const body = card.body ?? (await loadMessageBodies([card.item.itemId])).get(card.item.itemId);
    if (!body) {
      return null;
    }

    const currentUser = readCurrentUser();
    const to = card.source.to.length > 0 ? card.source.to : currentUser ? [currentUser] : [];
    if (to.length === 0) {
      return null;
    }

    return {
      itemId: card.source.itemId,
      subject: card.source.subject,
      from: card.source.from,
      to,
      cc: card.source.cc,
      sentAt: card.source.receivedAt,
      body,
      bodyFormat: "html",
    };
  }

  function toggleExpanded(itemId: string) {
    setExpandedId((current) => current === itemId ? null : itemId);
  }

  function renderCard(card: PriorityCard, review: boolean) {
    const open = expandedId === card.item.itemId;
    const detail = details[card.item.itemId];
    const received = formatReceived(card.item.receivedAt);
    return (
      <article className={`priority-card${card.read ? " is-read" : ""}${open ? " is-open" : ""}`} key={card.item.itemId}>
        <button
          type="button"
          className="priority-accordion"
          aria-expanded={open}
          onClick={() => toggleExpanded(card.item.itemId)}
        >
          <div className="priority-card-top">
            <span className="priority-chevron" aria-hidden="true">{open ? "▾" : "▸"}</span>
            {!review && (
              <span className={`priority priority-${priorityClass[card.priority] ?? "medium"}`}>
                {priorityLabel[card.priority] ?? "Orta"}
              </span>
            )}
            <span className={`read-flag ${card.read ? "read" : "unread"}`}>{card.read ? "Okundu" : "Okunmadı"}</span>
            <strong>{card.item.fromName || card.item.fromAddress}</strong>
            {received && <time className="priority-time">{received}</time>}
          </div>
          <h3>{card.item.subject}</h3>
        </button>
        {open && (
          <>
            <p>{card.verifying ? "Gövde doğrulanıyor…" : (detail?.result.summary ?? card.summary)}</p>
            <div className="priority-actions">
              <button type="button" onClick={() => void openInboxMessage(card.item.itemId).then(() => markRead(card.item.itemId)).catch((openError) => setError(errorMessage(openError)))}>Aç</button>
              <button
                type="button"
                disabled={markingId === card.item.itemId}
                onClick={() => void markReadInMailbox(card.item.itemId)}
              >
                {markingId === card.item.itemId ? "İşaretleniyor…" : "Okundu"}
              </button>
              <button type="button" onClick={() => dismissFromList(card.item.itemId)}>Sil</button>
            </div>
            {detail ? (
              <AnalysisResult result={detail.result} message={detail.message} />
            ) : (
              <p className="priority-status">Analiz sonucu hazırlanıyor…</p>
            )}
            <ReplyDraftPanel
              itemId={card.item.itemId}
              outputLanguage={outputLanguage}
              audience={{
                from: card.source?.from ?? { name: card.item.fromName, address: card.item.fromAddress },
                to: card.source?.to ?? [],
                cc: card.source?.cc ?? [],
              }}
              loadMessage={() => buildDraftMessage(card)}
              onSent={removeReplied}
            />
          </>
        )}
      </article>
    );
  }

  async function toggleFolderPanel() {
    const next = !foldersOpen;
    setFoldersOpen(next);
    if (!next || folders.length > 0 || foldersLoading) {
      return;
    }

    setFoldersLoading(true);
    setError(null);
    try {
      setFolders(await listMailFolders());
    } catch (folderError) {
      setError(errorMessage(folderError));
    } finally {
      setFoldersLoading(false);
    }
  }

  function toggleFolder(entryId: string) {
    const inboxId = folders.find((folder) => folder.kind === "inbox")?.entryId;
    const current = folderIds.length > 0 ? folderIds : inboxId ? [inboxId] : [];
    const next = current.includes(entryId)
      ? current.filter((id) => id !== entryId)
      : [...current, entryId].slice(0, 20);
    const kept = next.length === 0 && inboxId ? [inboxId] : next;
    const stored = inboxId && kept.length === 1 && kept[0] === inboxId ? [] : kept;
    setFolderIds(stored);
    localStorage.setItem(FOLDER_KEY, JSON.stringify(stored));
  }

  const personalCount = actionCards.filter((card) => card.personal).length;
  const readCount = actionCards.filter((card) => card.read).length;
  const range = rangeById(rangeId);
  const visibleCards = [...actionCards].sort(compareCards);

  return (
    <section className="priority-inbox">
      <div className="priority-heading">
        <div>
          <span className="eyebrow">Klasör taraması</span>
          <h2>Öncelikli mailler</h2>
          <p>
            {actionCards.length > 0
              ? `${personalCount} aksiyon · ${readCount} okundu. Kayıtlı analiz yeniden gönderilmez.`
              : "Seçtiğiniz aralıkta cevap veya aksiyon bekleyen mailler."}
          </p>
        </div>
        <button className="analyze-button" type="button" onClick={() => void handleAnalyze()} disabled={loading}>
          {loading ? "Analiz ediliyor…" : "Analiz et"}
        </button>
      </div>

      <div className="priority-filters">
        <label>
          <span>Zaman aralığı</span>
          <select
            value={rangeId}
            disabled={loading}
            onChange={(event) => {
              const next = event.target.value as InboxRangeId;
              setRangeId(next);
              localStorage.setItem(RANGE_KEY, next);
            }}
          >
            {inboxRanges.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>
        <label className="priority-check">
          <input
            type="checkbox"
            checked={unreadOnly}
            disabled={loading}
            onChange={(event) => {
              setUnreadOnly(event.target.checked);
              localStorage.setItem(UNREAD_KEY, String(event.target.checked));
            }}
          />
          <span>Yeni analizde yalnızca okunmamış</span>
        </label>
      </div>
      <div className="folder-picker">
        <button type="button" disabled={loading || foldersLoading} onClick={() => void toggleFolderPanel()}>
          Klasörler: {folderSummary(folderIds, folders)}
        </button>
        {foldersOpen && (
          <div className="folder-list" role="group" aria-label="Taranacak klasörler">
            {foldersLoading && <p>Klasörler okunuyor…</p>}
            {!foldersLoading && folders.length === 0 && <p>Klasör bulunamadı. Outlook’un açık olduğunu kontrol edin.</p>}
            {folders.map((folder) => {
              const checked = isFolderSelected(folder, folderIds, folders);
              return (
                <label key={folder.entryId}>
                  <input
                    type="checkbox"
                    checked={checked}
                    disabled={loading}
                    onChange={() => toggleFolder(folder.entryId)}
                  />
                  <span>
                    {folder.name}
                    <small>{folder.path}</small>
                  </span>
                </label>
              );
            })}
          </div>
        )}
      </div>

      {loading && <div className="progress" role="progressbar"><span /></div>}
      {statusText && <p className="priority-status">{statusText}</p>}
      {error && <div className="error" role="alert">{error}</div>}

      {searched && !loading && scannedCount === 0 && (
        <p className="priority-empty">
          {range.label} içinde, seçili klasörlerde{unreadOnly ? " okunmamış" : ""} mail yok.
        </p>
      )}

      {actionCards.length === 0 && !loading && scannedCount > 0 && (
        <p className="priority-empty">Bu aralıkta kişisel aksiyon gerektiren mail görünmüyor.</p>
      )}

      <div className="priority-list">
        {visibleCards.map((card) => renderCard(card, false))}
      </div>

      {reviewItems.length > 0 && (
        <div className="priority-review">
          <h3>Şüpheli, henüz netleşmeyen ({reviewItems.length})</h3>
          <div className="priority-list">
            {reviewItems.map(cardFromCache).map((card) => renderCard(card, true))}
          </div>
        </div>
      )}
    </section>
  );
}

async function loadBodiesChunked(itemIds: string[]): Promise<Map<string, string>> {
  const merged = new Map<string, string>();
  for (let index = 0; index < itemIds.length; index += 20) {
    const part = await loadMessageBodies(itemIds.slice(index, index + 20));
    part.forEach((body, itemId) => merged.set(itemId, body));
  }
  return merged;
}

async function hydrateReviews(
  cards: PriorityCard[],
  language: OutputLanguage,
  onReady: (itemId: string, detail: MailDetail) => void,
): Promise<void> {
  const bodies = await loadBodiesChunked(cards.map((card) => card.item.itemId));
  const currentUser = readCurrentUser();
  await mapPool(cards, 3, async (card) => {
    const body = bodies.get(card.item.itemId);
    if (!body) {
      return;
    }

    const to = card.source && card.source.to.length > 0 ? card.source.to : currentUser ? [currentUser] : [];
    if (to.length === 0) {
      return;
    }

    const message: MailMessage = {
      itemId: card.item.itemId,
      subject: card.item.subject,
      from: card.source?.from ?? { name: card.item.fromName, address: card.item.fromAddress },
      to,
      cc: card.source?.cc ?? [],
      sentAt: card.item.receivedAt,
      body,
      bodyFormat: "html",
    };
    const stored = loadMessageAnalysis(card.item.itemId, language);
    if (stored) {
      onReady(card.item.itemId, { result: stored.result, message });
      return;
    }

    try {
      const analysis = await analyzeMail({
        clientRequestId: crypto.randomUUID(),
        preferredLanguage: language,
        message,
        currentUser,
      });
      saveMessageAnalysis(card.item.itemId, language, analysis);
      onReady(card.item.itemId, { result: analysis, message });
    } catch {
      // The card stays on the short summary and can be retried by reopening the pane.
    }
  });
}

function formatReceived(value: string | null): string {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return date.toLocaleString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function cardFromCache(item: CachedPriorityMail): PriorityCard {
  return {
    item: reviewFromCache(item),
    source: {
      itemId: item.itemId,
      conversationId: null,
      subject: item.subject,
      from: { name: item.fromName, address: item.fromAddress },
      to: item.to ?? [],
      cc: item.cc ?? [],
      receivedAt: item.receivedAt,
      preview: item.summary,
      hasUnsubscribeHint: false,
      isAutoSubmitted: false,
      unread: !item.read,
    },
    summary: item.summary,
    priority: item.priority,
    personal: item.bucket === "action",
    verifying: false,
    read: item.read,
  };
}

function reviewFromCache(item: CachedPriorityMail): MailboxTriageItem {
  return {
    itemId: item.itemId,
    subject: item.subject,
    fromName: item.fromName,
    fromAddress: item.fromAddress,
    receivedAt: item.receivedAt,
    reason: item.summary,
    priority: item.priority,
  };
}

function cachedFromTriage(
  item: MailboxTriageItem,
  bucket: CachedPriorityMail["bucket"],
  source: InboxCandidate | undefined,
  language: string,
): CachedPriorityMail {
  return {
    itemId: item.itemId,
    language,
    bucket,
    subject: item.subject,
    fromName: item.fromName,
    fromAddress: item.fromAddress,
    to: source?.to ?? [],
    cc: source?.cc ?? [],
    receivedAt: item.receivedAt,
    summary: item.reason,
    priority: item.priority,
    read: source ? !source.unread : false,
  };
}

function withReadFlag(
  item: CachedPriorityMail,
  messages: InboxCandidate[],
  since: Date,
  unreadOnly: boolean,
): CachedPriorityMail {
  const live = messages.find((message) => message.itemId === item.itemId);
  if (live) {
    return { ...item, read: !live.unread, to: live.to, cc: live.cc };
  }

  const received = item.receivedAt ? new Date(item.receivedAt) : null;
  const inWindow = received !== null && !Number.isNaN(received.getTime()) && received >= since;
  if (unreadOnly && inWindow) {
    return { ...item, read: true };
  }

  return item;
}

function inRange(item: CachedPriorityMail, since: Date, seenIds: Set<string>, unreadOnly = false): boolean {
  if (unreadOnly && seenIds.size > 0 && !seenIds.has(item.itemId)) {
    return false;
  }

  if (seenIds.has(item.itemId)) {
    return true;
  }

  const received = item.receivedAt ? new Date(item.receivedAt) : null;
  return received !== null && !Number.isNaN(received.getTime()) && received >= since;
}

function compareCards(left: PriorityCard, right: PriorityCard): number {
  if (left.read !== right.read) {
    return left.read ? 1 : -1;
  }

  const rank: Record<string, number> = { urgent: 0, high: 1, normal: 2, low: 3 };
  const priority = (rank[left.priority] ?? 2) - (rank[right.priority] ?? 2);
  if (priority !== 0) {
    return priority;
  }

  return (right.item.receivedAt ?? "").localeCompare(left.item.receivedAt ?? "");
}

function errorMessage(error: unknown): string {
  if (error instanceof MailAnalysisApiError) {
    return error.code ? `${error.message} (${error.code})` : error.message;
  }

  return error instanceof Error ? error.message : "Gelen kutusu taranamadı.";
}

async function mapPool<T>(items: T[], limit: number, task: (item: T) => Promise<void>): Promise<void> {
  let index = 0;
  async function worker() {
    while (index < items.length) {
      const current = items[index];
      index += 1;
      await task(current);
    }
  }

  await Promise.all(Array.from({ length: Math.min(limit, items.length) }, () => worker()));
}
