import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import "./styles/app.css";

const root = document.getElementById("root");
if (!root) {
  throw new Error("Application root element was not found.");
}

Office.onReady((info) => {
  if (info.host !== Office.HostType.Outlook) {
    ReactDOM.createRoot(root).render(
      <React.StrictMode>
        <main><div className="error">Bu uygulama Outlook içinde çalıştırılmalıdır.</div></main>
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
