using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;

namespace OutlookMailAdviser.Api.Infrastructure;

public sealed class OutlookMailboxException(string message) : Exception(message);

public sealed partial class OutlookComMailbox(ILogger<OutlookComMailbox> logger)
{
    private const int MaxMessages = 250;

    private const int MaxScan = 2000;

    public List<OutlookMailFolder> ListFolders()
    {
        WriteLog("folders start");
        try
        {
            var folders = RunSta(ListFoldersCore);
            WriteLog($"folders done count={folders.Count}");
            return folders;
        }
        catch (Exception exception)
        {
            WriteLog($"folders failed {exception.Message}");
            throw;
        }
    }

    public List<OutlookInboxItem> List(DateTimeOffset since, bool unreadOnly, IReadOnlyList<string>? folderIds)
    {
        var ids = (folderIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        WriteLog($"inbox start since={since:O} unreadOnly={unreadOnly} folders={ids.Length}");
        try
        {
            var items = RunSta(() => ListCore(since, unreadOnly, ids));
            WriteLog($"inbox done count={items.Count}");
            LogListed(logger, items.Count);
            return items;
        }
        catch (Exception exception)
        {
            WriteLog($"inbox failed {exception.Message}");
            LogListFailed(logger, exception);
            throw;
        }
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Outlook inbox listed {Count} messages.")]
    private static partial void LogListed(ILogger logger, int count);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Warning, Message = "Outlook inbox list failed.")]
    private static partial void LogListFailed(ILogger logger, Exception exception);

    private static void WriteLog(string message)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
        Console.Out.Flush();
    }

    public List<OutlookBodyItem> GetBodies(IReadOnlyList<string> itemIds) =>
        RunSta(() =>
        {
            var outlook = Attach();
            try
            {
                dynamic application = outlook;
                dynamic mapi = application.GetNamespace("MAPI");
                var bodies = new List<OutlookBodyItem>(itemIds.Count);
                foreach (var itemId in itemIds)
                {
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }

                    object? item = null;
                    try
                    {
                        item = mapi.GetItemFromID(itemId);
                        if (item is null)
                        {
                            continue;
                        }

                        var html = ReadString(item, "HTMLBody");
                        var plain = ReadString(item, "Body");
                        var body = string.IsNullOrWhiteSpace(html) ? plain : html;
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            bodies.Add(new OutlookBodyItem(itemId, body.Length > 200_000 ? body[..200_000] : body));
                        }
                    }
                    catch (Exception exception) when (IsComAccessError(exception))
                    {
                        WriteLog($"body skipped {exception.GetType().Name}");
                    }
                    finally
                    {
                        Release(item);
                    }
                }

                return bodies;
            }
            finally
            {
                Release(outlook);
            }
        });

    public void Display(string itemId) =>
        RunSta(() =>
        {
            var outlook = Attach();
            try
            {
                dynamic application = outlook;
                dynamic mapi = application.GetNamespace("MAPI");
                object? item = mapi.GetItemFromID(itemId);
                if (item is null)
                {
                    throw new OutlookMailboxException("Mail Outlook'ta bulunamadı.");
                }

                try
                {
                    dynamic mail = item;
                    mail.Display();
                }
                finally
                {
                    Release(item);
                }

                return 0;
            }
            finally
            {
                Release(outlook);
            }
        });

    public void MarkRead(string itemId)
    {
        WriteLog("mark read start");
        try
        {
            RunSta(() =>
            {
                var outlook = Attach();
                try
                {
                    dynamic application = outlook;
                    dynamic mapi = application.GetNamespace("MAPI");
                    object? item = FindMail(mapi, itemId);
                    if (item is null)
                    {
                        WriteLog("mark read item missing");
                        return 0;
                    }

                    try
                    {
                        dynamic mail = item;
                        mail.UnRead = false;
                        mail.Save();
                    }
                    catch (COMException exception) when (exception.HResult == unchecked((int)0x8004010F))
                    {
                        WriteLog("mark read item missing");
                    }
                    catch (COMException exception)
                    {
                        throw new OutlookMailboxException(
                            $"Mail okundu işaretlenemedi. Outlook'un açık olduğunu kontrol edin. ({exception.ErrorCode})");
                    }
                    finally
                    {
                        Release(item);
                    }

                    return 0;
                }
                finally
                {
                    Release(outlook);
                }
            });
            WriteLog("mark read done");
        }
        catch (Exception exception)
        {
            WriteLog($"mark read failed {exception.Message}");
            throw;
        }
    }

    public void SaveReply(string itemId, string subject, string body)
    {
        WriteLog("reply save start");
        try
        {
            WriteReply(itemId, subject, body, send: false, replyAll: false);
            WriteLog("reply save done");
        }
        catch (Exception exception)
        {
            WriteLog($"reply save failed {exception.Message}");
            throw;
        }
    }

    public void SendReply(
        string itemId,
        string subject,
        string body,
        bool replyAll,
        IReadOnlyList<OutlookParticipant>? to,
        IReadOnlyList<OutlookParticipant>? cc)
    {
        WriteLog(replyAll ? "reply all start" : "reply send start");
        try
        {
            WriteReply(itemId, subject, body, send: true, replyAll, to, cc);
            WriteLog(replyAll ? "reply all done" : "reply send done");
        }
        catch (Exception exception)
        {
            WriteLog($"reply send failed {exception.Message}");
            throw;
        }
    }

    private static int WriteReply(
        string itemId,
        string subject,
        string body,
        bool send,
        bool replyAll,
        IReadOnlyList<OutlookParticipant>? to = null,
        IReadOnlyList<OutlookParticipant>? cc = null) =>
        RunSta(() =>
        {
            var outlook = Attach();
            try
            {
                dynamic application = outlook;
                dynamic mapi = application.GetNamespace("MAPI");
                object? item = mapi.GetItemFromID(itemId);
                if (item is null)
                {
                    throw new OutlookMailboxException("Mail Outlook'ta bulunamadı.");
                }

                object? reply = null;
                try
                {
                    dynamic mail = item;
                    try
                    {
                        reply = replyAll ? mail.ReplyAll() : mail.Reply();
                    }
                    catch (COMException exception)
                    {
                        throw new OutlookMailboxException(
                            $"Yanıt oluşturulamadı. Outlook'un açık olduğunu kontrol edin. ({exception.ErrorCode})");
                    }
                    if (reply is null)
                    {
                        throw new OutlookMailboxException("Yanıt oluşturulamadı.");
                    }

                    dynamic draft = reply;
                    var threadSubject = ThreadSubject(draft, mail);
                    var ignoredSubject = subject.Trim();
                    if (ignoredSubject.Length > 80)
                    {
                        ignoredSubject = ignoredSubject[..80];
                    }

                    WriteLog($"reply thread subject={threadSubject} draft-subject-ignored={ignoredSubject}");
                    var quoted = string.Empty;
                    try
                    {
                        quoted = Convert.ToString(draft.HTMLBody, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                    catch (COMException)
                    {
                        quoted = string.Empty;
                    }

                    ApplyBody(draft, body, quoted);
                    try
                    {
                        if (send)
                        {
                            if (replyAll && to is not null)
                            {
                                ReplaceRecipients(draft, to, cc ?? []);
                                WriteLog($"reply all recipients to={to.Count} cc={cc?.Count ?? 0}");
                            }

                            if (!ResolveRecipients(draft))
                            {
                                throw new OutlookMailboxException("Yanıtın alıcısı çözülemedi. Mail gönderilmedi.");
                            }

                            draft.Save();
                            var entryId = Convert.ToString(draft.EntryID, CultureInfo.InvariantCulture) ?? string.Empty;
                            var startedLocal = DateTime.Now.AddSeconds(-5);
                            draft.Send();
                            Release(reply);
                            reply = null;
                            object mapiObject = mapi;
                            object mailObject = mail;
                            if (!ConfirmSent(mapiObject, mailObject, entryId, threadSubject, startedLocal))
                            {
                                throw new OutlookMailboxException(SendFailureDetail(mapiObject, mailObject, entryId, threadSubject));
                            }
                        }
                        else
                        {
                            SaveDraft(draft, body);
                            KeepInDrafts(draft, (object)mapi, (object)mail, ref reply);
                        }
                    }
                    catch (COMException exception)
                    {
                        var detail = string.IsNullOrWhiteSpace(exception.Message) ? string.Empty : $" {exception.Message}";
                        throw new OutlookMailboxException(
                            send
                                ? $"Yanıt gönderilemedi. Outlook'un açık olduğunu kontrol edin. ({exception.ErrorCode}){detail}"
                                : $"Taslak kaydedilemedi. Outlook'un açık olduğunu kontrol edin. ({exception.ErrorCode}){detail}");
                    }
                }
                finally
                {
                    Release(reply);
                    Release(item);
                }

                return 0;
            }
            finally
            {
                Release(outlook);
            }
        });

    private static string ThreadSubject(dynamic draft, dynamic mail)
    {
        var current = (Convert.ToString(draft.Subject, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
        if (current.Length > 0)
        {
            return current.Length > 255 ? current[..255] : current;
        }

        var original = (Convert.ToString(mail.Subject, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
        var withPrefix = original.StartsWith("RE:", StringComparison.OrdinalIgnoreCase) ? original : "RE: " + original;
        if (withPrefix.Length > 255)
        {
            withPrefix = withPrefix[..255];
        }

        draft.Subject = withPrefix;
        return withPrefix;
    }

    private static void ApplyBody(dynamic draft, string body, string quoted)
    {
        try
        {
            draft.HTMLBody = InsertReplyHtml(quoted, body);
        }
        catch (COMException exception)
        {
            WriteLog($"reply html failed {exception.ErrorCode}");
            draft.Body = body;
        }
    }

    private static string InsertReplyHtml(string quoted, string body)
    {
        var fragment = ToReplyHtml(body);
        if (string.IsNullOrEmpty(quoted))
        {
            return fragment;
        }

        var bodyIndex = quoted.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyIndex >= 0)
        {
            var close = quoted.IndexOf('>', bodyIndex);
            if (close >= 0)
            {
                return quoted.Insert(close + 1, fragment);
            }
        }

        return fragment + quoted;
    }

    private static void SaveDraft(dynamic draft, string body)
    {
        try
        {
            draft.Save();
        }
        catch (COMException exception)
        {
            WriteLog($"reply save html failed {exception.ErrorCode}");
            draft.Body = body;
            draft.Save();
        }
    }

    private static void KeepInDrafts(dynamic draft, object mapi, object sourceMail, ref object? reply)
    {
        object folderObject;
        try
        {
            folderObject = DraftsFolder(mapi, sourceMail);
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            WriteLog($"reply save folder skipped {exception.Message}");
            return;
        }

        dynamic folder = folderObject;
        try
        {
            var parentId = Convert.ToString(draft.Parent.EntryID, CultureInfo.InvariantCulture) ?? string.Empty;
            var folderId = Convert.ToString(folder.EntryID, CultureInfo.InvariantCulture) ?? string.Empty;
            if (parentId.Length > 0 && string.Equals(parentId, folderId, StringComparison.OrdinalIgnoreCase))
            {
                WriteLog("reply save already in drafts");
                Release(folderObject);
                return;
            }
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException)
        {
            WriteLog("reply save folder compare skipped");
        }

        try
        {
            object? moved = draft.Move(folder);
            if (moved is not null)
            {
                Release(reply);
                reply = moved;
            }

            WriteLog("reply save moved");
        }
        catch (COMException exception)
        {
            WriteLog($"reply save move skipped {exception.ErrorCode}");
        }
        finally
        {
            Release(folderObject);
        }
    }

    private static void ReplaceRecipients(dynamic draft, IReadOnlyList<OutlookParticipant> to, IReadOnlyList<OutlookParticipant> cc)
    {
        dynamic recipients = draft.Recipients;
        var count = Convert.ToInt32(recipients.Count, CultureInfo.InvariantCulture);
        for (var index = count; index >= 1; index--)
        {
            dynamic recipient = recipients[index];
            try
            {
                recipient.Delete();
            }
            finally
            {
                Release(recipient);
            }
        }

        foreach (var person in to)
        {
            AddRecipient(recipients, person.Address, 1);
        }

        foreach (var person in cc)
        {
            AddRecipient(recipients, person.Address, 2);
        }
    }

    private static void AddRecipient(dynamic recipients, string address, int type)
    {
        dynamic recipient = recipients.Add(address);
        try
        {
            recipient.Type = type;
        }
        finally
        {
            Release(recipient);
        }
    }

    private static bool ResolveRecipients(dynamic draft)
    {
        try
        {
            dynamic recipients = draft.Recipients;
            return Convert.ToBoolean(recipients.ResolveAll(), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static bool ConfirmSent(object mapiObject, object sourceMail, string entryId, string subject, DateTime startedLocal)
    {
        dynamic mapi = mapiObject;
        StartSync(mapi);
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            if (IsSentItem(mapi, entryId) || FindRecentSent(mapiObject, sourceMail, subject, startedLocal))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return IsSentItem(mapi, entryId) || FindRecentSent(mapiObject, sourceMail, subject, startedLocal);
    }

    private static bool IsSentItem(dynamic mapi, string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        object? item = null;
        try
        {
            item = mapi.GetItemFromID(entryId);
            if (item is null)
            {
                return false;
            }

            dynamic mail = item;
            return Convert.ToBoolean(mail.Sent, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or FormatException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return false;
        }
        finally
        {
            Release(item);
        }
    }

    private static bool FindRecentSent(object mapiObject, object sourceMail, string subject, DateTime startedLocal)
    {
        object? folder = null;
        try
        {
            folder = DefaultFolder(mapiObject, sourceMail, 5);
            dynamic sent = folder;
            dynamic items = sent.Items;
            items.Sort("[SentOn]", true);
            object? item = items.GetFirst();
            var scanned = 0;
            while (item is not null && scanned < 15)
            {
                scanned++;
                object? next = null;
                try
                {
                    dynamic mail = item;
                    var itemSubject = (Convert.ToString(mail.Subject, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                    var sentOn = Convert.ToDateTime(mail.SentOn, CultureInfo.InvariantCulture);
                    if (string.Equals(itemSubject, subject, StringComparison.OrdinalIgnoreCase)
                        && sentOn >= startedLocal.AddMinutes(-1))
                    {
                        return true;
                    }

                    if (sentOn < startedLocal.AddMinutes(-2))
                    {
                        return false;
                    }

                    next = items.GetNext();
                }
                catch (Exception exception) when (exception is COMException or InvalidCastException or FormatException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    try
                    {
                        next = items.GetNext();
                    }
                    catch (COMException)
                    {
                        return false;
                    }
                }
                finally
                {
                    Release(item);
                }

                item = next;
            }

            return false;
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return false;
        }
        finally
        {
            Release(folder);
        }
    }

    private static string SendFailureDetail(object mapiObject, object sourceMail, string entryId, string subject)
    {
        if (IsStillUnsent(mapiObject, entryId) || FindInFolder(mapiObject, sourceMail, 4, subject))
        {
            return "Yanıt iletilemedi. Outlook Giden Kutusu'nda duruyor. Oraya bakın; tekrar Cevapla derseniz çift gidebilir.";
        }

        return "Yanıt iletilemedi. Gönderilmiş Öğeler klasöründe görünmedi.";
    }

    private static bool IsStillUnsent(object mapiObject, string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        dynamic mapi = mapiObject;
        object? item = null;
        try
        {
            item = mapi.GetItemFromID(entryId);
            if (item is null)
            {
                return false;
            }

            dynamic mail = item;
            return !Convert.ToBoolean(mail.Sent, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or FormatException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return false;
        }
        finally
        {
            Release(item);
        }
    }

    private static bool FindInFolder(object mapiObject, object sourceMail, int folderId, string subject)
    {
        object? folder = null;
        try
        {
            folder = DefaultFolder(mapiObject, sourceMail, folderId);
            dynamic box = folder;
            dynamic items = box.Items;
            object? item = items.GetFirst();
            var scanned = 0;
            while (item is not null && scanned < 20)
            {
                scanned++;
                object? next = null;
                try
                {
                    dynamic mail = item;
                    var itemSubject = (Convert.ToString(mail.Subject, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
                    if (string.Equals(itemSubject, subject, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    next = items.GetNext();
                }
                catch (Exception exception) when (exception is COMException or InvalidCastException or FormatException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    try
                    {
                        next = items.GetNext();
                    }
                    catch (COMException)
                    {
                        return false;
                    }
                }
                finally
                {
                    Release(item);
                }

                item = next;
            }

            return false;
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return false;
        }
        finally
        {
            Release(folder);
        }
    }

    private static void StartSync(dynamic mapi)
    {
        try
        {
            dynamic syncObjects = mapi.SyncObjects;
            var count = Convert.ToInt32(syncObjects.Count, CultureInfo.InvariantCulture);
            for (var index = 1; index <= count; index++)
            {
                try
                {
                    dynamic sync = syncObjects.Item(index);
                    sync.Start();
                }
                catch (COMException)
                {
                    // One account failing to sync does not hide the send result.
                }
            }
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException or FormatException)
        {
            try
            {
                mapi.SendAndReceive(false);
            }
            catch (COMException)
            {
                // Confirmation still checks Sent Items.
            }
        }
    }

    private static object DefaultFolder(object mapiObject, object sourceMail, int folderId)
    {
        dynamic mapi = mapiObject;
        dynamic mail = sourceMail;
        try
        {
            dynamic parent = mail.Parent;
            dynamic store = parent.Store;
            return store.GetDefaultFolder(folderId);
        }
        catch (Exception exception) when (exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            return mapi.GetDefaultFolder(folderId);
        }
    }

    private static object DraftsFolder(object mapi, object sourceMail) =>
        DefaultFolder(mapi, sourceMail, 16);

    private static string ToReplyHtml(string body)
    {
        if (body.Contains("<p", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<br", StringComparison.OrdinalIgnoreCase)
            || body.Contains("<div", StringComparison.OrdinalIgnoreCase))
        {
            return body;
        }

        var encoded = WebUtility.HtmlEncode(body)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal);
        return $"<div>{encoded}</div>";
    }

    private static List<OutlookMailFolder> ListFoldersCore()
    {
        var outlook = Attach();
        try
        {
            dynamic application = outlook;
            dynamic mapi = application.GetNamespace("MAPI");
            var inboxId = string.Empty;
            object? inbox = null;
            try
            {
                inbox = mapi.GetDefaultFolder(6);
                if (inbox is not null)
                {
                    inboxId = ReadString(inbox, "EntryID");
                }
            }
            catch (Exception exception) when (IsComAccessError(exception))
            {
                inboxId = string.Empty;
            }
            finally
            {
                Release(inbox);
            }

            var results = new List<OutlookMailFolder>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dynamic stores = mapi.Stores;
            var storeCount = Convert.ToInt32(stores.Count, CultureInfo.InvariantCulture);
            for (var index = 1; index <= storeCount && index <= 6 && results.Count < 80; index++)
            {
                object? store = null;
                object? root = null;
                try
                {
                    store = stores.Item(index);
                    dynamic currentStore = store;
                    root = currentStore.GetRootFolder();
                    if (root is not null)
                    {
                        WalkFolders(root, results, seen, inboxId, 0);
                    }
                }
                catch (Exception exception) when (IsComAccessError(exception))
                {
                    WriteLog($"folders store skipped {exception.Message}");
                }
                finally
                {
                    Release(root);
                    Release(store);
                }
            }

            results.Sort(static (left, right) =>
            {
                var leftInbox = string.Equals(left.Kind, "inbox", StringComparison.Ordinal) ? 0 : 1;
                var rightInbox = string.Equals(right.Kind, "inbox", StringComparison.Ordinal) ? 0 : 1;
                var kind = leftInbox.CompareTo(rightInbox);
                return kind != 0
                    ? kind
                    : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }
        finally
        {
            Release(outlook);
        }
    }

    private static void WalkFolders(
        object folderObject,
        List<OutlookMailFolder> results,
        HashSet<string> seen,
        string inboxId,
        int depth)
    {
        if (depth > 4 || results.Count >= 80)
        {
            return;
        }

        dynamic folder = folderObject;
        if (depth > 0 && IsMailFolder(folderObject))
        {
            var entryId = ReadString(folderObject, "EntryID");
            var name = ReadString(folderObject, "Name");
            if (!string.IsNullOrWhiteSpace(entryId) && !string.IsNullOrWhiteSpace(name) && seen.Add(entryId))
            {
                var path = FolderPath(folderObject, name);
                var kind = entryId.Equals(inboxId, StringComparison.OrdinalIgnoreCase) ? "inbox" : "mail";
                results.Add(new OutlookMailFolder(entryId, name, path, kind));
            }
        }

        object? children = null;
        try
        {
            children = folder.Folders;
            if (children is null)
            {
                return;
            }

            dynamic collection = children;
            var count = Convert.ToInt32(collection.Count, CultureInfo.InvariantCulture);
            for (var index = 1; index <= count && results.Count < 80; index++)
            {
                object? child = null;
                try
                {
                    child = collection.Item(index);
                    if (child is not null)
                    {
                        WalkFolders(child, results, seen, inboxId, depth + 1);
                    }
                }
                catch (Exception exception) when (IsComAccessError(exception))
                {
                    // One folder does not hide the rest of the mailbox.
                }
                finally
                {
                    Release(child);
                }
            }
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            WriteLog($"folders walk skipped {exception.Message}");
        }
        finally
        {
            Release(children);
        }
    }

    private static bool IsMailFolder(object folder)
    {
        try
        {
            dynamic current = folder;
            object? value = current.DefaultItemType;
            return Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0;
        }
        catch (Exception exception) when (IsComAccessError(exception) || exception is InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static string FolderPath(object folder, string name)
    {
        try
        {
            dynamic current = folder;
            object? value = current.FolderPath;
            var raw = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var path = raw.Trim().TrimStart('\\').Replace('\\', '/');
            return string.IsNullOrWhiteSpace(path) ? name : path;
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            return name;
        }
    }

    private static List<OutlookInboxItem> ListCore(DateTimeOffset since, bool unreadOnly, IReadOnlyList<string> folderIds)
    {
        var outlook = Attach();
        var opened = new List<object>();
        try
        {
            dynamic application = outlook;
            dynamic mapi = application.GetNamespace("MAPI");
            if (folderIds.Count == 0)
            {
                object? inbox = mapi.GetDefaultFolder(6);
                if (inbox is not null)
                {
                    opened.Add(inbox);
                }
            }
            else
            {
                foreach (var folderId in folderIds)
                {
                    try
                    {
                        object? folder = mapi.GetFolderFromID(folderId);
                        if (folder is not null)
                        {
                            opened.Add(folder);
                        }
                    }
                    catch (Exception exception) when (IsComAccessError(exception))
                    {
                        WriteLog($"inbox folder missing {exception.Message}");
                    }
                }

                if (opened.Count == 0)
                {
                    object? inbox = mapi.GetDefaultFolder(6);
                    if (inbox is not null)
                    {
                        opened.Add(inbox);
                    }
                }
            }

            var sinceLocal = since.LocalDateTime;
            var results = new List<OutlookInboxItem>(MaxMessages);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var scanned = 0;
            foreach (var folder in opened)
            {
                if (results.Count >= MaxMessages || scanned >= MaxScan)
                {
                    break;
                }

                ScanFolder(mapi, folder, sinceLocal, unreadOnly, results, seen, ref scanned);
            }

            WriteLog($"inbox table scanned={scanned} kept={results.Count} folders={opened.Count}");
            return results;
        }
        finally
        {
            foreach (var folder in opened)
            {
                Release(folder);
            }

            Release(outlook);
        }
    }

    private static void ScanFolder(
        object mapiObject,
        object folderObject,
        DateTime sinceLocal,
        bool unreadOnly,
        List<OutlookInboxItem> results,
        HashSet<string> seen,
        ref int scanned)
    {
        dynamic mapi = mapiObject;
        dynamic folder = folderObject;
        object? tableObject = null;
        try
        {
            tableObject = folder.GetTable(string.Empty, 0);
            if (tableObject is null)
            {
                return;
            }

            dynamic table = tableObject;
            AddColumn(table, "ReceivedTime");
            table.Sort("ReceivedTime", true);
            while (!table.EndOfTable && results.Count < MaxMessages && scanned < MaxScan)
            {
                scanned++;
                object? row = null;
                object? item = null;
                try
                {
                    row = table.GetNextRow();
                    if (row is null)
                    {
                        continue;
                    }

                    var entryId = ReadRowString(row, "EntryID");
                    if (string.IsNullOrWhiteSpace(entryId) || !seen.Add(entryId))
                    {
                        continue;
                    }

                    item = mapi.GetItemFromID(entryId);
                    if (item is null)
                    {
                        continue;
                    }

                    if (!TryReadRowTime(row, out var received) && !TryReadTime(item, out received))
                    {
                        continue;
                    }

                    if (received < sinceLocal)
                    {
                        break;
                    }

                    var unread = ReadBool(item, "UnRead");
                    if (unreadOnly && !unread)
                    {
                        continue;
                    }

                    var mapped = TryMap(item, received, unread);
                    if (mapped is not null)
                    {
                        results.Add(mapped);
                    }
                }
                catch (Exception exception) when (IsComAccessError(exception))
                {
                    // One unreadable row does not hide the rest of the folder.
                }
                finally
                {
                    Release(item);
                    Release(row);
                }
            }
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            WriteLog($"inbox folder skipped {exception.Message}");
        }
        finally
        {
            Release(tableObject);
        }
    }

    private static OutlookInboxItem? TryMap(object item, DateTime received, bool unread)
    {
        var address = ReadSmtp(item);
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var (to, cc) = ReadRecipients(item);
        var preview = ReadString(item, "Body");
        if (preview.Length > 400)
        {
            preview = preview[..400];
        }

        return new OutlookInboxItem(
            ReadString(item, "EntryID"),
            NullIfEmpty(ReadString(item, "ConversationID")),
            NullIfEmpty(ReadString(item, "Subject")) ?? "(Konu yok)",
            new OutlookParticipant(NullIfEmpty(ReadString(item, "SenderName")), address),
            to,
            cc,
            new DateTimeOffset(DateTime.SpecifyKind(received, DateTimeKind.Local)),
            preview,
            false,
            false,
            unread);
    }

    private static (IReadOnlyList<OutlookParticipant> To, IReadOnlyList<OutlookParticipant> Cc) ReadRecipients(object mailItem)
    {
        dynamic item = mailItem;
        var to = new List<OutlookParticipant>();
        var cc = new List<OutlookParticipant>();
        try
        {
            dynamic recipients = item.Recipients;
            var count = Convert.ToInt32(recipients.Count, CultureInfo.InvariantCulture);
            for (var index = 1; index <= count && index <= 32; index++)
            {
                dynamic recipient = recipients[index];
                try
                {
                    var name = NullIfEmpty(ReadString(recipient, "Name"));
                    var address = ReadRecipientSmtp(recipient);
                    if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
                    {
                        address = name;
                    }

                    if (string.IsNullOrWhiteSpace(address))
                    {
                        continue;
                    }

                    var participant = new OutlookParticipant(name, address);
                    var type = Convert.ToInt32(recipient.Type, CultureInfo.InvariantCulture);
                    if (type == 2)
                    {
                        cc.Add(participant);
                    }
                    else if (type == 1)
                    {
                        to.Add(participant);
                    }
                }
                finally
                {
                    Release(recipient);
                }
            }
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            return (to, cc);
        }

        return (to, cc);
    }

    private static string ReadSmtp(object mailItem)
    {
        dynamic item = mailItem;
        var smtp = ReadProperty(item, "http://schemas.microsoft.com/mapi/proptag/0x5D01001F");
        return string.IsNullOrWhiteSpace(smtp) ? ReadString(item, "SenderEmailAddress") : smtp;
    }

    private static string ReadRecipientSmtp(object recipientItem)
    {
        dynamic recipient = recipientItem;
        var smtp = ReadProperty(recipient, "http://schemas.microsoft.com/mapi/proptag/0x39FE001F");
        if (smtp.Contains('@'))
        {
            return smtp;
        }

        return ReadString(recipient, "Address");
    }

    private static string ReadProperty(object target, string property)
    {
        dynamic item = target;
        try
        {
            object? value = item.PropertyAccessor.GetProperty(property);
            return value?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            return string.Empty;
        }
    }

    private static string ReadString(object target, string property)
    {
        dynamic item = target;
        try
        {
            object? value = property switch
            {
                "HTMLBody" => item.HTMLBody,
                "Body" => item.Body,
                "EntryID" => item.EntryID,
                "ConversationID" => item.ConversationID,
                "Subject" => item.Subject,
                "SenderName" => item.SenderName,
                "SenderEmailAddress" => item.SenderEmailAddress,
                "Name" => item.Name,
                "Address" => item.Address,
                _ => null
            };
            return value?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            return string.Empty;
        }
    }

    private static bool ReadBool(object target, string property)
    {
        dynamic item = target;
        try
        {
            if (property != "UnRead")
            {
                return false;
            }

            return Convert.ToBoolean(item.UnRead, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (IsComAccessError(exception) || exception is InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static string StoreId(object store)
    {
        dynamic current = store;
        object? value = current.StoreID;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static object? FindMail(dynamic mapi, string itemId)
    {
        var storeIds = new List<string>();
        try
        {
            dynamic inbox = mapi.GetDefaultFolder(6);
            var inboxStoreId = StoreId(inbox);
            if (!string.IsNullOrWhiteSpace(inboxStoreId))
            {
                storeIds.Add(inboxStoreId);
            }
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            // The default store is tried again below.
        }

        try
        {
            dynamic stores = mapi.Stores;
            var count = Convert.ToInt32(stores.Count, CultureInfo.InvariantCulture);
            for (var index = 1; index <= count && index <= 12; index++)
            {
                object? store = null;
                try
                {
                    store = stores.Item(index);
                    var storeId = StoreId(store);
                    if (!string.IsNullOrWhiteSpace(storeId) && !storeIds.Exists(existing => string.Equals(existing, storeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        storeIds.Add(storeId);
                    }
                }
                catch (Exception exception) when (IsComAccessError(exception))
                {
                    // One store does not block the others.
                }
                finally
                {
                    Release(store);
                }
            }
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            // Fall back to the id without a store.
        }

        foreach (var storeId in storeIds)
        {
            try
            {
                object? item = mapi.GetItemFromID(itemId, storeId);
                if (item is not null)
                {
                    return item;
                }
            }
            catch (Exception exception) when (IsComAccessError(exception))
            {
                // Try the next store.
            }
        }

        try
        {
            return mapi.GetItemFromID(itemId);
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            return null;
        }
    }

    private static void AddColumn(dynamic table, string column)
    {
        try
        {
            table.Columns.Add(column);
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            // The default table already contains some columns.
        }
    }

    private static bool TryReadRowTime(object row, out DateTime received)
    {
        received = DateTime.MinValue;
        var value = RowValue(row, "ReceivedTime");
        if (value is null)
        {
            return false;
        }

        try
        {
            received = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            return received > DateTime.MinValue.AddDays(1);
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static string ReadRowString(object row, string column) =>
        RowValue(row, column)?.ToString()?.Trim() ?? string.Empty;

    private static object? RowValue(object row, string column)
    {
        dynamic record = row;
        try
        {
            return record.Item(column);
        }
        catch (Exception exception) when (IsComAccessError(exception))
        {
            try
            {
                return record[column];
            }
            catch (Exception inner) when (IsComAccessError(inner))
            {
                return null;
            }
        }
    }

    private static bool TryReadTime(object target, out DateTime received)
    {
        dynamic item = target;
        received = DateTime.MinValue;
        try
        {
            received = Convert.ToDateTime(item.ReceivedTime, CultureInfo.InvariantCulture);
            return received > DateTime.MinValue.AddDays(1);
        }
        catch (Exception exception) when (IsComAccessError(exception) || exception is InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static bool IsComAccessError(Exception exception) =>
        exception is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;

    private static object Attach()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new OutlookMailboxException("Gelen kutusu yalnızca Windows üzerinde Outlook ile okunur.");
        }

        var outlookType = Type.GetTypeFromProgID("Outlook.Application");
        if (outlookType is null)
        {
            throw new OutlookMailboxException(
                "Bu bilgisayarda klasik Outlook bulunamadı. Gelen kutusu listesi Outlook açıkken çalışır.");
        }

        try
        {
            return Activator.CreateInstance(outlookType)
                ?? throw new OutlookMailboxException("Outlook başlatılamadı.");
        }
        catch (COMException exception)
        {
            throw new OutlookMailboxException(
                $"Outlook gelen kutusuna erişilemedi. Outlook'un açık olduğunu kontrol edin. ({exception.ErrorCode})");
        }
    }

    private static int busy;

    private static T RunSta<T>(Func<T> action)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new OutlookMailboxException("Gelen kutusu yalnızca Windows üzerinde Outlook ile okunur.");
        }

        if (Interlocked.CompareExchange(ref busy, 1, 0) != 0)
        {
            throw new OutlookMailboxException(
                "Outlook hâlâ önceki okumayla meşgul. İzin penceresi açıksa İzin Ver'e basın, sonra tekrar deneyin.");
        }

        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(45)))
        {
            throw new OutlookMailboxException(
                "Outlook zamanında yanıt vermedi. İzin penceresi açıksa İzin Ver'e basın.");
        }

        if (failure is not null)
        {
            throw failure;
        }

        return result!;
    }

    private static void Release(object? comObject)
    {
        if (!OperatingSystem.IsWindows() || comObject is null)
        {
            return;
        }

        if (Marshal.IsComObject(comObject))
        {
            try
            {
                Marshal.ReleaseComObject(comObject);
            }
            catch (COMException)
            {
                // Send() already released the reply item.
            }
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed record OutlookParticipant(string? Name, string Address);

public sealed record OutlookMailFolder(string EntryId, string Name, string Path, string Kind);

public sealed record OutlookInboxItem(
    string ItemId,
    string? ConversationId,
    string Subject,
    OutlookParticipant From,
    IReadOnlyList<OutlookParticipant> To,
    IReadOnlyList<OutlookParticipant> Cc,
    DateTimeOffset? ReceivedAt,
    string Preview,
    bool HasUnsubscribeHint,
    bool IsAutoSubmitted,
    bool Unread);

public sealed record OutlookBodyItem(string ItemId, string Body);
