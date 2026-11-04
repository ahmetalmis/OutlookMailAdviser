interface StoredToken {
  accessToken: string;
  expiresAt: number;
}

let memoryToken: StoredToken | null = null;

const setupMessage = [
  "Kurum, posta kutusunun tamamını okuyan eklenti iznini engelledi. Liste ReadItem ile kurulur; gelen kutusu ayrı bir Microsoft oturumuyla okunur.",
  "addin/.env.local dosyasına şunları yazıp eklenti sunucusunu yeniden başlatın:",
  "VITE_GRAPH_CLIENT_ID=<Azure uygulama kimliği>",
  "VITE_GRAPH_TENANT_ID=<kiracı kimliği>",
  "Azure uygulaması SPA olmalı, yönlendirme adresi https://localhost:3000/auth.html, temsilci izni Mail.Read.",
].join(" ");

export function graphClientConfigured(): boolean {
  return Boolean(import.meta.env.VITE_GRAPH_CLIENT_ID?.trim());
}

export async function ensureGraphToken(): Promise<string> {
  if (memoryToken && memoryToken.expiresAt > Date.now() + 60_000) {
    return memoryToken.accessToken;
  }

  const clientId = import.meta.env.VITE_GRAPH_CLIENT_ID?.trim();
  if (!clientId) {
    throw new Error(setupMessage);
  }

  const tenantId = import.meta.env.VITE_GRAPH_TENANT_ID?.trim() || "organizations";
  const access = await requestToken(clientId, tenantId);
  memoryToken = access;
  return access.accessToken;
}

function requestToken(clientId: string, tenantId: string): Promise<StoredToken> {
  const dialogUrl = new URL(`${window.location.origin}/auth.html`);
  dialogUrl.searchParams.set("clientId", clientId);
  dialogUrl.searchParams.set("tenantId", tenantId);

  return new Promise((resolve, reject) => {
    Office.context.ui.displayDialogAsync(
      dialogUrl.toString(),
      { height: 60, width: 30, displayInIframe: false },
      (result) => {
        if (result.status !== Office.AsyncResultStatus.Succeeded || !result.value) {
          reject(new Error(result.error?.message || "Microsoft oturum penceresi açılamadı."));
          return;
        }

        const dialog = result.value;
        let settled = false;
        const finish = (handler: () => void) => {
          if (settled) {
            return;
          }

          settled = true;
          dialog.close();
          handler();
        };

        dialog.addEventHandler(Office.EventType.DialogMessageReceived, (event) => {
          finish(() => {
            if (!("message" in event)) {
              reject(new Error("Microsoft oturumu tamamlanamadı."));
              return;
            }

            try {
              const payload = JSON.parse(event.message) as { accessToken?: string; expiresIn?: number; error?: string };
              if (!payload.accessToken) {
                reject(new Error(payload.error || "Microsoft oturumu reddedildi."));
                return;
              }

              resolve({
                accessToken: payload.accessToken,
                expiresAt: Date.now() + (payload.expiresIn ?? 3600) * 1000,
              });
            } catch {
              reject(new Error("Microsoft oturum yanıtı okunamadı."));
            }
          });
        });
        dialog.addEventHandler(Office.EventType.DialogEventReceived, () => {
          finish(() => reject(new Error("Microsoft oturum penceresi kapatıldı.")));
        });
      },
    );
  });
}
