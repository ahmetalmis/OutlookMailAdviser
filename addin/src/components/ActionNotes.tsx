import { useCallback, useEffect, useRef, useState } from "react";
import { completeActionNote, createActionNote, editActionNote, listActionNotes } from "../api/actionNotesApi";
import { useLanguage } from "../i18n/LanguageContext";
import { errorText, UiError, type MessageKey } from "../i18n/messages";
import type { ActionNote, NewActionNote } from "../types";

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  selection: NewActionNote | null;
  onSelectionClear: () => void;
}
type Editor = { note: NewActionNote; editing: boolean };
export function ActionNotes({ open, onOpenChange, selection, onSelectionClear }: Props) {
  const { t, number, language } = useLanguage();
  const [notes, setNotes] = useState<ActionNote[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [saveError, setSaveError] = useState<unknown>(null);
  const [notice, setNotice] = useState<MessageKey | null>(null);
  const [editor, setEditor] = useState<Editor | null>(null);
  const [completed, setCompleted] = useState(false);
  const [search, setSearch] = useState("");
  const [owner, setOwner] = useState("*");
  const [busy, setBusy] = useState(false);
  const busyRef = useRef(false);
  const requestVersion = useRef(0);
  const mounted = useRef(true);

  const refresh = useCallback(async (signal?: AbortSignal) => {
    const version = ++requestVersion.current;
    setLoading(true);
    try {
      const records = await listActionNotes(signal);
      if (mounted.current && !signal?.aborted && version === requestVersion.current) {
        setNotes(records); setLoaded(true); setError(null);
      }
    } catch (reason) {
      if (mounted.current && !signal?.aborted && version === requestVersion.current) setError(reason);
    } finally {
      if (mounted.current && !signal?.aborted && version === requestVersion.current) setLoading(false);
    }
  }, []);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    void refresh(controller.signal);
    const onFocus = () => { if (!busyRef.current) void refresh(controller.signal); };
    window.addEventListener("focus", onFocus);
    return () => { controller.abort(); mounted.current = false; window.removeEventListener("focus", onFocus); };
  }, [open, refresh]);
  useEffect(() => {
    if (selection) {
      setEditor({ note: { ...selection }, editing: false }); setSaveError(null); setNotice(null);
      setCompleted(false); setOwner("*"); setSearch(""); onSelectionClear();
    }
  }, [selection, onSelectionClear]);

  async function save() {
    if (!editor || busyRef.current) return;
    if (!editor.note.mailSubject.trim() || !editor.note.description.trim()) {
      setSaveError(new UiError("noteFieldsRequired")); return;
    }
    busyRef.current = true; setBusy(true); setSaveError(null); setNotice(null);
    const note = { ...editor.note, mailSubject: editor.note.mailSubject.trim(), description: editor.note.description.trim(),
      owner: editor.note.owner?.trim() || null, dueDate: editor.note.dueDate || null };
    try {
      const saved = editor.editing ? await editActionNote(note.id, note) : await createActionNote(note);
      if (!mounted.current) return;
      ++requestVersion.current;
      setNotes(current => [saved, ...current.filter(record => record.id !== saved.id)]);
      setEditor(null); setOwner("*"); setNotice("noteSaved");
      await refresh();
    } catch (reason) { if (mounted.current) setSaveError(reason); }
    finally { busyRef.current = false; if (mounted.current) setBusy(false); }
  }
  async function changeCompletion(note: ActionNote) {
    if (busyRef.current) return;
    busyRef.current = true; setBusy(true); setError(null); setNotice(null);
    try {
      const saved = await completeActionNote(note.id, !note.isCompleted);
      if (!mounted.current) return;
      ++requestVersion.current;
      setNotes(current => current.map(record => record.id === saved.id ? saved : record));
      setNotice(saved.isCompleted ? "noteCompleted" : "noteReopened");
      await refresh();
    } catch (reason) { if (mounted.current) setError(reason); }
    finally { busyRef.current = false; if (mounted.current) setBusy(false); }
  }
  const activeCount = notes.filter(note => !note.isCompleted).length;
  const inStatus = notes.filter(note => note.isCompleted === completed);
  const owners = [...new Set(notes.map(note => note.owner ?? ""))].sort((a, b) => a.localeCompare(b, language));
  const visible = inStatus.filter(note => (owner === "*" || JSON.stringify(note.owner ?? "") === owner)
    && `${note.mailSubject} ${note.description}`.toLocaleLowerCase(language).includes(search.trim().toLocaleLowerCase(language)))
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt) || a.id.localeCompare(b.id));
  const formatDate = (date: string) => new Date(date.length > 10 ? date : `${date}T12:00:00`).toLocaleDateString(language === "tr" ? "tr-TR" : "en-US");
  function changeField(field: keyof NewActionNote, value: string) {
    setEditor(current => current ? { ...current, note: { ...current.note, [field]: value } } : null);
  }
  return <section className="action-tracker" aria-label={t("myActions")}>
    <nav className="action-navigation" aria-label={t("myActions")}>
      <button type="button" aria-pressed={open} onClick={() => { setNotice(null); onOpenChange(!open); }}>
        {open ? t("backToEmail") : `${t("myActions")} (${loaded ? number(activeCount) : "…"})`}
      </button>
    </nav>
    {open && <div className="action-notes-view">
      <h2>{t("myActions")} ({loaded ? number(activeCount) : "…"})</h2>
      <p className="notes-help">{t("notesLocalInfo")}</p>
      {notice && <p role="status">{t(notice)}</p>}
      {editor && <form className="action-note-editor" onSubmit={event => { event.preventDefault(); void save(); }}>
        <h3>{t(editor.editing ? "editActionNote" : "newActionNote")}</h3>
        <label className="draft-field"><span>{t("mailSubject")}</span><input required maxLength={1000} value={editor.note.mailSubject}
          disabled={busy} onChange={event => changeField("mailSubject", event.target.value)} /></label>
        <label className="draft-field"><span>{t("action")}</span><textarea required maxLength={8000} rows={4} value={editor.note.description}
          disabled={busy} onChange={event => changeField("description", event.target.value)} /></label>
        <label className="draft-field"><span>{t("actionOwner")}</span><input maxLength={1000} value={editor.note.owner ?? ""}
          disabled={busy} placeholder={t("unknownOwner")} onChange={event => changeField("owner", event.target.value)} /></label>
        <label className="draft-field"><span>{t("dueDate")}</span><input type="date" value={editor.note.dueDate ?? ""}
          disabled={busy} onChange={event => changeField("dueDate", event.target.value)} /></label>
        {saveError != null && <p className="error" role="alert">{errorText(saveError, t, "action_note_request_error")}</p>}
        <div className="note-buttons"><button type="submit" disabled={busy}>{t(busy ? "savingNote" : "saveNote")}</button>
          <button type="button" disabled={busy} onClick={() => { setEditor(null); setSaveError(null); }}>{t("cancel")}</button></div>
      </form>}
      <div className="note-buttons">
        <button type="button" aria-pressed={!completed} onClick={() => { setCompleted(false); setOwner("*"); }}>{t("activeNotes")} ({number(activeCount)})</button>
        <button type="button" aria-pressed={completed} onClick={() => { setCompleted(true); setOwner("*"); }}>{t("completedNotes")} ({number(notes.length - activeCount)})</button>
      </div>
      <label className="draft-field"><span>{t("searchNotes")}</span><input type="search" value={search} onChange={event => setSearch(event.target.value)} /></label>
      <label className="draft-field"><span>{t("filterOwner")}</span><select value={owner} onChange={event => setOwner(event.target.value)}>
        <option value="*">{t("allOwners")}</option>{owners.map(name => <option key={name} value={JSON.stringify(name)}>{name || t("unknownOwner")}</option>)}
      </select></label>
      <button type="button" disabled={loading || busy} onClick={() => void refresh()}>{t("refreshNotes")}</button>
      {loading && <p role="status">{t("loadingNotes")}</p>}
      {error != null && <p className="error" role="alert">{errorText(error, t, "action_note_request_error")}</p>}
      {loaded && !loading && visible.length === 0 && <p>{t(search || owner !== "*" ? "noMatchingNotes" : completed ? "noCompletedNotes" : "noActionNotes")}</p>}
      <ul className="action-note-list">{visible.map(note => <li key={note.id} className="action-note-card">
        <h3>{note.mailSubject}</h3><p className="note-description">{note.description}</p>
        <p><strong>{t("actionOwner")}:</strong> {note.owner || t("unknownOwner")}</p>
        {note.dueDate && <p>{t("dueDateLabel")}: {formatDate(note.dueDate)}</p>}
        {note.completedAt && <p>{t("completedOn")}: {formatDate(note.completedAt)}</p>}
        <div className="note-buttons">
          <button type="button" disabled={busy || editor !== null} onClick={() => { setEditor({ note: { ...note }, editing: true }); setSaveError(null); setNotice(null); }}>{t("editNote")}</button>
          <button type="button" disabled={busy || editor !== null} onClick={() => void changeCompletion(note)}>{t(note.isCompleted ? "reopenNote" : "completeNote")}</button>
        </div>
      </li>)}</ul>
    </div>}
  </section>;
}
