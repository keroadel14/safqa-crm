import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2 } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api, problem } from "../lib/api";
import { money } from "../lib/cn";
interface Product { id: string; name: string; sku: string; unitPrice: number; vatRate: number }
interface Line { productId?: string; description: string; quantity: number; unitPrice: number; discountPct: number; vatRate: number }
interface QuoteRow { id: string; number: string; dealName: string; status: string; total: number; currency: string }
const blank = (): Line => ({ description: "", quantity: 1, unitPrice: 0, discountPct: 0, vatRate: 15 });
const r2 = (v: number) => Math.round((v + Number.EPSILON) * 100) / 100;

export default function QuotesPage() {
  const { t, i18n } = useTranslation(); const qc = useQueryClient(); const lang = i18n.language;
  const { data: board } = useQuery({ queryKey: ["board"], queryFn: async () => (await api.get<{ deals: { id: string; name: string; currency: string }[] }>("/deals/board")).data });
  const { data: products = [] } = useQuery({ queryKey: ["products"], queryFn: async () => (await api.get<Product[]>("/products")).data });
  const { data: recent = [] } = useQuery({ queryKey: ["quotes"], queryFn: async () => (await api.get<QuoteRow[]>("/quotes")).data });
  const [dealId, setDealId] = useState(""); const [valid, setValid] = useState(new Date(Date.now() + 14 * 864e5).toISOString().slice(0, 10));
  const [terms, setTerms] = useState(""); const [lines, setLines] = useState<Line[]>([blank()]);
  const cur = board?.deals.find(d => d.id === dealId)?.currency ?? "SAR";

  // Mirrors the server's QuoteCalculator (round per line, total = subtotal - discount + VAT)
  const tot = useMemo(() => {
    let sub = 0, disc = 0, vat = 0;
    for (const l of lines) { const g = r2(l.quantity * l.unitPrice), d = r2(g * l.discountPct / 100), v = r2((g - d) * l.vatRate / 100); sub += g; disc += d; vat += v; }
    return { sub, disc, vat, total: sub - disc + vat };
  }, [lines]);

  const download = async (id: string, number: string) => {
    const res = await api.get(`/quotes/${id}/pdf`, { params: { lang }, responseType: "blob" });
    const url = URL.createObjectURL(res.data); const a = document.createElement("a"); a.href = url; a.download = `${number}.pdf`; a.click(); URL.revokeObjectURL(url);
  };
  const save = useMutation({
    mutationFn: async () => (await api.post<{ id: string; number: string; needsApproval: boolean }>("/quotes", { dealId, validUntil: valid, terms: terms || null, lines })).data,
    onSuccess: async q => { if (q.needsApproval) toast.warning(t("needsApproval")); await download(q.id, q.number); void qc.invalidateQueries({ queryKey: ["quotes"] }); setLines([blank()]); },
    onError: e => toast.error(problem(e)),
  });
  const set = (i: number, p: Partial<Line>) => setLines(ls => ls.map((l, j) => j === i ? { ...l, ...p } : l));
  const pick = (i: number, id: string) => { const p = products.find(x => x.id === id); set(i, p ? { productId: p.id, description: p.name, unitPrice: p.unitPrice, vatRate: p.vatRate } : { productId: undefined }); };
  const num = "input tabular";

  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <section className="card space-y-4">
        <h1 className="text-xl font-semibold">{t("quoteBuilder")}</h1>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="text-sm">{t("deal")}<select className="input mt-1" value={dealId} onChange={e => setDealId(e.target.value)}><option value="" />{board?.deals.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
          <label className="text-sm">{t("validUntil")}<input className="input mt-1" type="date" value={valid} onChange={e => setValid(e.target.value)} /></label>
        </div>
        <div className="space-y-3">
          {lines.map((l, i) => (
            <div key={i} className="grid grid-cols-2 gap-2 rounded-lg border border-slate-200 p-3 dark:border-slate-800 md:grid-cols-12 md:border-0 md:p-0">
              <select className="input col-span-2 md:col-span-3" aria-label={t("product")} value={l.productId ?? ""} onChange={e => pick(i, e.target.value)}><option value="">—</option>{products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select>
              <input className="input col-span-2 md:col-span-3" placeholder={t("product")} value={l.description} onChange={e => set(i, { description: e.target.value })} />
              <input className={`${num} md:col-span-1`} type="number" min={0} step="any" aria-label={t("qty")} placeholder={t("qty")} value={l.quantity} onChange={e => set(i, { quantity: +e.target.value })} />
              <input className={`${num} md:col-span-2`} type="number" min={0} step="any" aria-label={t("price")} placeholder={t("price")} value={l.unitPrice} onChange={e => set(i, { unitPrice: +e.target.value })} />
              <input className={`${num} md:col-span-1`} type="number" min={0} max={100} step="any" aria-label={t("discount")} placeholder={t("discount")} value={l.discountPct} onChange={e => set(i, { discountPct: +e.target.value })} />
              <div className="flex gap-2 md:col-span-2"><input className={num} type="number" min={0} max={30} step="any" aria-label={t("vat")} placeholder={t("vat")} value={l.vatRate} onChange={e => set(i, { vatRate: +e.target.value })} />
                <button type="button" className="btn-ghost px-3" aria-label="Remove" disabled={lines.length === 1} onClick={() => setLines(ls => ls.filter((_, j) => j !== i))}><Trash2 size={16} /></button></div>
            </div>
          ))}
          <button type="button" className="btn-ghost" onClick={() => setLines(ls => [...ls, blank()])}><Plus size={16} />{t("addLine")}</button>
        </div>
        <textarea className="input h-20 py-2" placeholder={t("terms")} value={terms} onChange={e => setTerms(e.target.value)} />
        <div className="ms-auto w-full max-w-xs space-y-1 text-sm">
          {([[t("subtotal"), tot.sub], [t("discountTotal"), -tot.disc], [t("vatTotal"), tot.vat]] as const).map(([k, v]) => <div key={k} className="flex justify-between"><span>{k}</span><span className="tabular">{money(v, cur, lang)}</span></div>)}
          <div className="flex justify-between border-t border-slate-300 pt-1 text-base font-semibold text-primary dark:border-slate-700"><span>{t("grandTotal")}</span><span className="tabular">{money(tot.total, cur, lang)}</span></div>
        </div>
        <button className="btn-primary w-full sm:w-auto" disabled={!dealId || lines.some(l => !l.description.trim()) || save.isPending} onClick={() => save.mutate()}>{t("saveDownload")}</button>
      </section>
      <aside className="card h-fit">
        <h2 className="mb-2 font-semibold">{t("recentQuotes")}</h2>
        <ul className="divide-y divide-slate-200 dark:divide-slate-800">
          {recent.map(q => (
            <li key={q.id}><button onClick={() => void download(q.id, q.number)} className="flex min-h-[44px] w-full items-center justify-between gap-2 py-2 text-start text-sm hover:text-primary">
              <span><span className="font-mono" dir="ltr">{q.number}</span><span className="block text-xs text-slate-500">{q.dealName} · {q.status}</span></span><span className="tabular">{money(q.total, q.currency, lang)}</span></button></li>
          ))}
          {recent.length === 0 && <li className="py-3 text-sm text-slate-500">{t("noData")}</li>}
        </ul>
      </aside>
    </div>
  );
}
