import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { Sheet } from "../components/Sheet";
import { api, downloadFile, problem } from "../lib/api";
import { useAuth } from "../lib/auth";
interface Lead { id: string; firstName: string; lastName: string; companyName?: string; email?: string; phone?: string; source: string; status: string }
interface Page { items: Lead[]; total: number; page: number; pageSize: number }
const SOURCES = ["Website", "Referral", "Exhibition", "Social", "ColdCall", "Partner", "Other"];
const STATUSES = ["New", "Contacted", "Qualified", "Unqualified", "Converted"];

export default function LeadsPage() {
  const { t } = useTranslation(); const qc = useQueryClient(); const [sp, setSp] = useSearchParams();
  const [status, setStatus] = useState(""); const [page, setPage] = useState(1); const [open, setOpen] = useState(sp.get("new") === "1");
  const { user } = useAuth(); const canExport = user?.role !== "Rep";
  const q = sp.get("q") ?? "";
  const { data } = useQuery({ queryKey: ["leads", status, page, q], queryFn: async () => (await api.get<Page>("/leads", { params: { status: status || undefined, page, q: q || undefined } })).data });

  const convert = useMutation({
    mutationFn: async (l: Lead) => {
      const board = (await api.get<{ stages: { id: string }[] }>("/deals/board")).data;
      const close = new Date(Date.now() + 30 * 864e5).toISOString().slice(0, 10);
      return api.post(`/leads/${l.id}/convert`, { contact: { mode: "New" }, company: { mode: "New", name: l.companyName }, deal: { name: l.companyName ?? `${l.firstName} ${l.lastName}`, amount: 0, stageId: board.stages[0].id, expectedClose: close } });
    },
    onSuccess: () => { toast.success("✓"); void qc.invalidateQueries({ queryKey: ["leads"] }); void qc.invalidateQueries({ queryKey: ["board"] }); },
    onError: e => toast.error(problem(e)),
  });

  const [f, setF] = useState({ firstName: "", lastName: "", email: "", phone: "", companyName: "", source: "Website" });
  const create = useMutation({
    mutationFn: () => api.post("/leads", { ...f, email: f.email || null, phone: f.phone || null, companyName: f.companyName || null }),
    onSuccess: () => { setOpen(false); setF({ firstName: "", lastName: "", email: "", phone: "", companyName: "", source: "Website" }); void qc.invalidateQueries({ queryKey: ["leads"] }); },
    onError: e => toast.error(problem(e)),
  });
  const setLeadStatus = useMutation({
    mutationFn: (v: { id: string; status: string; unqualifiedReason?: string }) => api.put(`/leads/${v.id}/status`, { status: v.status, unqualifiedReason: v.unqualifiedReason }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["leads"] }), onError: e => toast.error(problem(e)),
  });
  const statusSel = (l: Lead) => l.status === "Converted" ? <span>{l.status}</span> : (
    <select className="input w-auto py-1" aria-label={t("status")} value={l.status} onChange={e => {
      const s = e.target.value; let reason: string | undefined;
      if (s === "Unqualified") { reason = window.prompt(t("unqualifiedReason"))?.trim(); if (!reason) return; }
      setLeadStatus.mutate({ id: l.id, status: s, unqualifiedReason: reason });
    }}>{STATUSES.filter(s => s !== "Converted").map(s => <option key={s}>{s}</option>)}</select>
  );
  const submit = (e: FormEvent) => { e.preventDefault(); create.mutate(); };
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="me-auto text-xl font-semibold md:text-2xl">{t("leads")}</h1>
        <select className="input w-auto" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }} aria-label={t("status")}>
          <option value="">{t("status")}</option>{STATUSES.map(s => <option key={s}>{s}</option>)}
        </select>
        {q && <button className="btn-ghost" onClick={() => setSp({})}>{q} ✕</button>}
        {canExport && <button className="btn-ghost" onClick={() => void downloadFile("/leads/export", "leads.csv").catch(e => toast.error(problem(e)))}>{t("exportCsv")}</button>}
        <button className="btn-primary" onClick={() => setOpen(true)}>{t("newLead")}</button>
      </div>

      {/* Phones: cards. md+: table */}
      <ul className="space-y-2 md:hidden">
        {data?.items.map(l => (
          <li key={l.id} className="card">
            <div className="font-medium">{l.firstName} {l.lastName}</div>
            <div className="text-sm text-slate-500">{l.companyName}</div>
            <div className="mt-1 flex items-center justify-between text-sm">{statusSel(l)}{l.phone && <a className="btn-ghost" href={`tel:${l.phone}`}>{l.phone}</a>}</div>
            {l.status === "Qualified" && <button className="btn-primary mt-2 w-full" disabled={convert.isPending} onClick={() => convert.mutate(l)}>{t("convert")}</button>}
          </li>
        ))}
      </ul>
      <div className="card hidden overflow-x-auto p-0 md:block">
        <table className="w-full text-sm">
          <thead className="bg-[#EEF3F5] text-start dark:bg-slate-900"><tr>{[t("name"), t("company"), t("email"), t("source"), t("status"), ""].map((h, i) => <th key={i} className="px-4 py-3 text-start font-medium">{h}</th>)}</tr></thead>
          <tbody>
            {data?.items.map(l => (
              <tr key={l.id} className="border-t border-slate-200 dark:border-slate-800">
                <td className="px-4 py-3">{l.firstName} {l.lastName}</td><td className="px-4 py-3">{l.companyName}</td>
                <td className="px-4 py-3" dir="ltr">{l.email}</td><td className="px-4 py-3">{l.source}</td><td className="px-4 py-3">{statusSel(l)}</td>
                <td className="px-4 py-3 text-end">{l.status === "Qualified" && <button className="btn-ghost" disabled={convert.isPending} onClick={() => convert.mutate(l)}>{t("convert")}</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data && data.items.length === 0 && <p className="card text-center text-slate-500">{t("noData")}</p>}
      <div className="flex items-center justify-center gap-3">
        <button className="btn-ghost" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button><span className="tabular text-sm">{page} / {pages}</span>
        <button className="btn-ghost" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>›</button>
      </div>

      <Sheet open={open} onClose={() => setOpen(false)} title={t("newLead")}>
        <form onSubmit={submit} className="grid gap-3 sm:grid-cols-2">
          <input className="input" required placeholder={t("firstName")} value={f.firstName} onChange={e => setF({ ...f, firstName: e.target.value })} />
          <input className="input" required placeholder={t("lastName")} value={f.lastName} onChange={e => setF({ ...f, lastName: e.target.value })} />
          <input className="input" type="email" dir="ltr" placeholder={t("email")} value={f.email} onChange={e => setF({ ...f, email: e.target.value })} />
          <input className="input" type="tel" dir="ltr" placeholder={t("phone")} value={f.phone} onChange={e => setF({ ...f, phone: e.target.value })} />
          <input className="input" placeholder={t("company")} value={f.companyName} onChange={e => setF({ ...f, companyName: e.target.value })} />
          <select className="input" value={f.source} onChange={e => setF({ ...f, source: e.target.value })}>{SOURCES.map(s => <option key={s}>{s}</option>)}</select>
          <button className="btn-primary sm:col-span-2" disabled={create.isPending}>{t("save")}</button>
        </form>
      </Sheet>
    </div>
  );
}
