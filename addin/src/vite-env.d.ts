/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_GRAPH_CLIENT_ID?: string;
  readonly VITE_GRAPH_TENANT_ID?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
