import "@fontsource-variable/inter";
import "@fontsource-variable/noto-sans-arabic";
import "./index.css";
import "./i18n";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { Toaster } from "sonner";
import App from "./App";
import { ShellProvider } from "./app/ThemeLangProvider";
import { AuthProvider } from "./lib/auth";
import { NotificationsProvider } from "./lib/notifications";

const qc = new QueryClient({ defaultOptions: { queries: { staleTime: 15_000, retry: 1, refetchOnWindowFocus: true } } });
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={qc}>
      <ShellProvider>
        <AuthProvider>
          <NotificationsProvider>
            <BrowserRouter><App /></BrowserRouter>
            <Toaster richColors position="top-center" />
          </NotificationsProvider>
        </AuthProvider>
      </ShellProvider>
    </QueryClientProvider>
  </React.StrictMode>,
);
