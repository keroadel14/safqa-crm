import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { tokens } from "./api";
import { useAuth } from "./auth";
export interface Note { id: number; text: string; at: Date; read: boolean }
export type HubStatus = "connecting" | "connected" | "reconnecting" | "closed";
const Ctx = createContext<{ notes: Note[]; markRead(): void; status: HubStatus }>({ notes: [], markRead() {}, status: "closed" });
export const useNotifications = () => useContext(Ctx);

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [notes, setNotes] = useState<Note[]>([]);
  const [status, setStatus] = useState<HubStatus>("closed");

  useEffect(() => {
    if (!user) { setStatus("closed"); return; }
    let disposed = false;
    const push = (text: string) => { toast(text); setNotes(n => [{ id: Date.now() + Math.random(), text, at: new Date(), read: false }, ...n].slice(0, 30)); };
    const conn = new HubConnectionBuilder()
      .withUrl("/hubs/notifications", { accessTokenFactory: () => tokens.access ?? "" })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning).build();
      conn.on("dealMoved", (p: { name: string; by: string }) => { if (p.by !== user.id) push(`Deal moved: ${p.name}`); });
conn.on("passwordReset", (p: { message: string }) => push(p.message || "User reset their password"));

    conn.on("leadAssigned", (p: { name: string }) => push(`New lead: ${p.name}`));
    conn.on("dealMoved", (p: { name: string; by: string }) => { if (p.by !== user.id) push(`Deal moved: ${p.name}`); });
    conn.onreconnecting(() => !disposed && setStatus("reconnecting"));
    conn.onreconnected(() => !disposed && setStatus("connected"));
    conn.onclose(() => !disposed && setStatus("closed"));          // retries exhausted – UI keeps working without realtime

    const start = async (attempt = 0): Promise<void> => {
      if (disposed || conn.state !== HubConnectionState.Disconnected) return;
      setStatus("connecting");
      try { await conn.start(); if (!disposed) setStatus("connected"); }
      catch { if (!disposed) { setStatus("closed"); setTimeout(() => void start(attempt + 1), Math.min(30000, 2000 * 2 ** attempt)); } }
    };
    void start();
    return () => { disposed = true; void conn.stop(); };
  }, [user]);

  return <Ctx.Provider value={{ notes, status, markRead: () => setNotes(n => n.map(x => ({ ...x, read: true }))) }}>{children}</Ctx.Provider>;
}
