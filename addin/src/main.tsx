import { LanguageProvider, useLanguage } from "./i18n/LanguageContext";
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./styles/app.css";

const root = document.getElementById("root");
if (!root) {
  throw new Error("Application root element was not found.");
}

function OutlookRequired() {
  const { t } = useLanguage();
  return <main><div className="error">{t("outlookRequired")}</div></main>;
}

Office.onReady((info) => {
  if (info.host !== Office.HostType.Outlook) {
    ReactDOM.createRoot(root).render(
      <React.StrictMode>
        <LanguageProvider><OutlookRequired /></LanguageProvider>
      </React.StrictMode>,
    );
    return;
  }

  ReactDOM.createRoot(root).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>,
  );
});
