import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { api } from "../lib/api";
interface Hit { type: "lead" | "deal" | "quote"; id: string; title: string; subtitle?: string }

export function CommandPalette({ open, onClose }: { open: boolean; onClose(): void }) {
  const { t } = useTranslation(); const nav = useNavigate();
  const [q, setQ] = useState(""); const [dq, setDq] = useState("");
  useEffect(() => { const id = setTimeout(() => setDq(q), 250); return () => clearTimeout(id); }, [q]);
  useEffect(() => { if (!open) { setQ(""); setDq(""); } }, [open]);
  const { data = [] } = useQuery({ queryKey: ["search", dq], enabled: open && dq.trim().length >= 2, queryFn: async () => (await api.get<Hit[]>("/search", { params: { q: dq } })).data });
  if (!open) return null;
  const go = (h: Hit) => { onClose(); nav(h.type === "lead" ? `/leads?q=${encodeURIComponent(h.title)}` : h.type === "deal" ? "/deals" : "/quotes"); };
  return (
    <div className="fixed inset-0 z-[60] flex items-start justify-center bg-black/40 p-3 pt-[10vh]" onMouseDown={e => e.target === e.currentTarget && onClose()} onKeyDown={e => e.key === "Escape" && onClose()}>
      <div role="dialog" aria-modal="true" className="w-full max-w-xl overflow-hidden rounded-xl bg-card shadow-xl dark:bg-card-dark">
        <div className="flex items-center gap-2 border-b border-slate-200 px-3 dark:border-slate-800">
          <Search size={18} className="text-slate-400" />
          <input autoFocus value={q} onChange={e => setQ(e.target.value)} placeholder={t("search")} className="h-12 w-full bg-transparent text-[15px] outline-none" />
        </div>
        <ul className="max-h-[50vh] overflow-y-auto p-1">
          {dq.trim().length >= 2 && data.length === 0 && <li className="p-4 text-sm text-slate-500">{t("noResults")}</li>}
          {data.map(h => (
            <li key={h.type + h.id}>
              <button onClick={() => go(h)} className="flex min-h-[44px] w-full items-center justify-between gap-3 rounded-lg px-3 py-2 text-start hover:bg-slate-100 dark:hover:bg-slate-800">
                <span className="truncate">{h.title}</span>
                <span className="shrink-0 text-xs uppercase text-slate-500">{h.type}{h.subtitle ? ` · ${h.subtitle}` : ""}</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
