import { useQuery } from "@tanstack/react-query";
import { AlertTriangle, CalendarClock, Clock, UserPlus } from "lucide-react";
import { useTranslation } from "react-i18next";
import { api } from "../lib/api";
import { money } from "../lib/cn";
interface Dash { overdueTasks: number; meetingsToday: number; uncontactedLeads: number; uncontactedBreached: number; stalledDeals: number; weightedForecast: number; openPipeline: number; currency: string; forecast: { month: string; weighted: number; total: number }[] }

export default function DashboardPage() {
  const { t, i18n } = useTranslation();
  const { data } = useQuery({ queryKey: ["dashboard"], queryFn: async () => (await api.get<Dash>("/dashboard")).data });
  if (!data) return <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">{[...Array(4)].map((_, i) => <div key={i} className="h-28 animate-pulse rounded-xl bg-slate-200 dark:bg-slate-800" />)}</div>;
  const max = Math.max(1, ...data.forecast.map(f => f.total));
  const cards = [
    { l: t("overdue"), v: data.overdueTasks, i: AlertTriangle, c: "text-red-600" },
    { l: t("meetings"), v: data.meetingsToday, i: CalendarClock, c: "text-blue-600" },
    { l: t("uncontacted"), v: data.uncontactedLeads, i: UserPlus, c: "text-primary", s: `${data.uncontactedBreached} ${t("breached")}` },
    { l: t("stalled"), v: data.stalledDeals, i: Clock, c: "text-accent" },
  ];
  return (
    <div className="space-y-4 md:space-y-6">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {cards.map(c => (
          <div key={c.l} className="card">
            <c.i size={20} className={c.c} />
            <div className="tabular mt-2 text-3xl font-semibold">{c.v}</div>
            <div className="text-sm text-slate-600 dark:text-slate-400">{c.l}</div>
            {c.s && <div className="text-xs text-red-600">{c.s}</div>}
          </div>
        ))}
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <div className="card"><div className="text-sm text-slate-500">{t("weighted")}</div><div className="tabular text-2xl font-semibold text-primary">{money(data.weightedForecast, data.currency, i18n.language)}</div></div>
        <div className="card"><div className="text-sm text-slate-500">{t("pipeline")}</div><div className="tabular text-2xl font-semibold">{money(data.openPipeline, data.currency, i18n.language)}</div></div>
      </div>
      <section className="card">
        <h2 className="mb-4 font-semibold">{t("forecast")}</h2>
        <div className="flex h-48 items-end gap-2 sm:gap-4" role="img" aria-label={t("forecast")}>
          {data.forecast.map(f => (
            <div key={f.month} className="flex h-full flex-1 flex-col justify-end gap-1" title={`${f.month}: ${money(f.weighted, data.currency, i18n.language)}`}>
              <div className="relative w-full rounded-t bg-slate-200 dark:bg-slate-700" style={{ height: `${(f.total / max) * 100}%` }}>
                <div className="absolute inset-x-0 bottom-0 rounded-t bg-primary" style={{ height: f.total ? `${(f.weighted / f.total) * 100}%` : 0 }} />
              </div>
              <div className="text-center text-[11px] text-slate-500" dir="ltr">{f.month.slice(2)}</div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
