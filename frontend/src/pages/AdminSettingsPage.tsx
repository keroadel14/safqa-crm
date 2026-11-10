import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowDown, ArrowUp } from "lucide-react";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Sheet } from "../components/Sheet";
import { api, problem } from "../lib/api";
import { cn } from "../lib/cn";

interface UserRow { id: string; email: string; fullName: string; role: string; teamId?: string; isActive: boolean; discountLimitPct: number }
interface Team { id: string; name: string }
interface Stage { id: string; name: string; nameAr: string; position: number; kind: string; defaultProbability: number }
interface Audit { id: number; entityType: string; action: string; at: string; oldValues?: string; newValues?: string }
const TABS = ["users", "teams", "stages", "audit"] as const;
const Table = ({ head, children }: { head: string[]; children: ReactNode }) => (
  <div className="card overflow-x-auto p-0"><table className="w-full min-w-[32rem] text-sm"><thead className="bg-[#EEF3F5] dark:bg-slate-900"><tr>{head.map(h => <th key={h} className="px-4 py-3 text-start font-medium">{h}</th>)}</tr></thead><tbody className="[&>tr]:border-t [&>tr]:border-slate-200 dark:[&>tr]:border-slate-800 [&_td]:px-4 [&_td]:py-3">{children}</tbody></table></div>
);

function Users() {
  const { t } = useTranslation(); const qc = useQueryClient(); const [open, setOpen] = useState(false);
  const { data = [] } = useQuery({ queryKey: ["a-users"], queryFn: async () => (await api.get<UserRow[]>("/admin/users")).data });
  const [f, setF] = useState({ email: "", password: "", fullName: "", role: "Rep", discountLimitPct: 10 });
  const create = useMutation({ mutationFn: () => api.post("/admin/users", f), onSuccess: () => { setOpen(false); void qc.invalidateQueries({ queryKey: ["a-users"] }); }, onError: e => toast.error(problem(e)) });
  const toggle = useMutation({
    mutationFn: (u: UserRow) => api.put(`/admin/users/${u.id}`, { fullName: u.fullName, role: u.role, teamId: u.teamId ?? null, isActive: !u.isActive, discountLimitPct: u.discountLimitPct }),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["a-users"] }), onError: e => toast.error(problem(e)),
  });
  return (
    <div className="space-y-3">
      <button className="btn-primary" onClick={() => setOpen(true)}>{t("add")}</button>
      <Table head={[t("name"), t("email"), t("role"), t("active")]}>
        {data.map(u => <tr key={u.id}><td>{u.fullName}</td><td dir="ltr">{u.email}</td><td>{u.role}</td><td><input type="checkbox" className="h-5 w-5" checked={u.isActive} onChange={() => toggle.mutate(u)} aria-label={t("active")} /></td></tr>)}
      </Table>
      <Sheet open={open} onClose={() => setOpen(false)} title={t("users")}>
        <form className="grid gap-3" onSubmit={e => { e.preventDefault(); create.mutate(); }}>
          <input className="input" required placeholder={t("name")} value={f.fullName} onChange={e => setF({ ...f, fullName: e.target.value })} />
          <input className="input" required type="email" dir="ltr" placeholder={t("email")} value={f.email} onChange={e => setF({ ...f, email: e.target.value })} />
          <input className="input" required type="password" dir="ltr" minLength={8} placeholder={t("password")} value={f.password} onChange={e => setF({ ...f, password: e.target.value })} />
          <select className="input" value={f.role} onChange={e => setF({ ...f, role: e.target.value })}>{["Rep", "Manager", "Admin"].map(r => <option key={r}>{r}</option>)}</select>
          <input className="input" type="number" min={0} max={100} aria-label="Discount limit %" value={f.discountLimitPct} onChange={e => setF({ ...f, discountLimitPct: +e.target.value })} />
          <button className="btn-primary" disabled={create.isPending}>{t("save")}</button>
        </form>
      </Sheet>
    </div>
  );
}

function Teams() {
  const { t } = useTranslation(); const qc = useQueryClient(); const [name, setName] = useState("");
  const { data = [] } = useQuery({ queryKey: ["a-teams"], queryFn: async () => (await api.get<Team[]>("/admin/teams")).data });
  const inv = () => void qc.invalidateQueries({ queryKey: ["a-teams"] });
  const add = useMutation({ mutationFn: () => api.post("/admin/teams", { name, managerId: null }), onSuccess: () => { setName(""); inv(); }, onError: e => toast.error(problem(e)) });
  const del = useMutation({ mutationFn: (id: string) => api.delete(`/admin/teams/${id}`), onSuccess: inv, onError: e => toast.error(problem(e)) });
  return (
    <div className="space-y-3">
      <form className="flex gap-2" onSubmit={e => { e.preventDefault(); if (name.trim()) add.mutate(); }}><input className="input" placeholder={t("name")} value={name} onChange={e => setName(e.target.value)} /><button className="btn-primary">{t("add")}</button></form>
      <Table head={[t("name"), ""]}>{data.map(x => <tr key={x.id}><td>{x.name}</td><td className="text-end"><button className="btn-ghost" onClick={() => del.mutate(x.id)}>✕</button></td></tr>)}</Table>
    </div>
  );
}

function Stages() {
  const { t, i18n } = useTranslation(); const qc = useQueryClient();
  const { data = [] } = useQuery({ queryKey: ["a-stages"], queryFn: async () => (await api.get<Stage[]>("/admin/stages")).data });
  const [n, setN] = useState({ name: "", nameAr: "", p: 30 });
  const inv = () => { void qc.invalidateQueries({ queryKey: ["a-stages"] }); void qc.invalidateQueries({ queryKey: ["board"] }); };
  const reorder = useMutation({ mutationFn: (ids: string[]) => api.put("/admin/stages/reorder", { stageIds: ids }), onSuccess: inv, onError: e => toast.error(problem(e)) });
  const add = useMutation({ mutationFn: () => api.post("/admin/stages", { name: n.name, nameAr: n.nameAr, defaultProbability: n.p, kind: "Open", isActive: true }), onSuccess: () => { setN({ name: "", nameAr: "", p: 30 }); inv(); }, onError: e => toast.error(problem(e)) });
  const prob = useMutation({ mutationFn: (s: Stage) => api.put(`/admin/stages/${s.id}`, { name: s.name, nameAr: s.nameAr, defaultProbability: s.defaultProbability, kind: s.kind, isActive: true }), onSuccess: inv, onError: e => toast.error(problem(e)) });
  const move = (i: number, d: number) => { const ids = data.map(s => s.id); [ids[i], ids[i + d]] = [ids[i + d], ids[i]]; reorder.mutate(ids); };
  return (
    <div className="space-y-3">
      <form className="grid gap-2 sm:grid-cols-[1fr_1fr_6rem_auto]" onSubmit={e => { e.preventDefault(); add.mutate(); }}>
        <input className="input" required placeholder="Name" value={n.name} onChange={e => setN({ ...n, name: e.target.value })} />
        <input className="input" required placeholder="الاسم" value={n.nameAr} onChange={e => setN({ ...n, nameAr: e.target.value })} />
        <input className="input" type="number" min={0} max={100} value={n.p} onChange={e => setN({ ...n, p: +e.target.value })} /><button className="btn-primary">{t("add")}</button>
      </form>
      <ul className="space-y-2">
        {data.map((s, i) => (
          <li key={s.id} className="card flex items-center gap-2 p-3">
            <span className="tabular w-6 text-slate-500">{s.position}</span><span className="min-w-0 flex-1 truncate">{i18n.language === "ar" ? s.nameAr : s.name} <span className="text-xs text-slate-500">({s.kind})</span></span>
            <input className="input w-20" type="number" min={0} max={100} defaultValue={s.defaultProbability} aria-label="%" onBlur={e => { const v = +e.target.value; if (v !== s.defaultProbability) prob.mutate({ ...s, defaultProbability: v }); }} />
            <button className="btn-ghost px-3" disabled={i === 0} onClick={() => move(i, -1)} aria-label="Up"><ArrowUp size={16} /></button>
            <button className="btn-ghost px-3" disabled={i === data.length - 1} onClick={() => move(i, 1)} aria-label="Down"><ArrowDown size={16} /></button>
          </li>
        ))}
      </ul>
    </div>
  );
}

function AuditLog() {
  const { t } = useTranslation(); const [page, setPage] = useState(1);
  const { data } = useQuery({ queryKey: ["a-audit", page], queryFn: async () => (await api.get<{ items: Audit[]; total: number; pageSize: number }>("/admin/audit-logs", { params: { page } })).data });
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  return (
    <div className="space-y-3">
      <Table head={["At", "Entity", "Action", "Changes"]}>
        {data?.items.map(a => <tr key={a.id}><td className="whitespace-nowrap tabular">{new Date(a.at).toLocaleString()}</td><td>{a.entityType}</td><td>{a.action}</td><td className="max-w-xs truncate font-mono text-xs" dir="ltr" title={a.newValues}>{a.newValues ?? a.oldValues}</td></tr>)}
      </Table>
      <div className="flex items-center justify-center gap-3"><button className="btn-ghost" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹</button><span className="tabular text-sm">{page} / {pages}</span><button className="btn-ghost" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>›</button></div>
      <span className="sr-only">{t("audit")}</span>
    </div>
  );
}

export default function AdminSettingsPage() {
  const { t } = useTranslation(); const [tab, setTab] = useState<(typeof TABS)[number]>("users");
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold md:text-2xl">{t("settings")}</h1>
      <div role="tablist" className="-mx-3 flex gap-2 overflow-x-auto px-3 sm:mx-0 sm:px-0">
        {TABS.map(k => <button key={k} role="tab" aria-selected={tab === k} onClick={() => setTab(k)} className={cn("min-h-[44px] shrink-0 rounded-full border px-4 text-sm md:min-h-10", tab === k ? "border-primary bg-primary text-white" : "border-slate-300 dark:border-slate-700")}>{t(k)}</button>)}
      </div>
      {tab === "users" && <Users />}{tab === "teams" && <Teams />}{tab === "stages" && <Stages />}{tab === "audit" && <AuditLog />}
    </div>
  );
}
