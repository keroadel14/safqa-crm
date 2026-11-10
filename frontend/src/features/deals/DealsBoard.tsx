import { DndContext, DragOverlay, KeyboardSensor, PointerSensor, TouchSensor, closestCorners, useDraggable, useDroppable, useSensor, useSensors, type DragEndEvent, type DragStartEvent } from "@dnd-kit/core";
import { sortableKeyboardCoordinates } from "@dnd-kit/sortable";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Sheet } from "../../components/Sheet";
import { api, problem } from "../../lib/api";
import { cn, money } from "../../lib/cn";

export interface Stage { id: string; name: string; nameAr: string; position: number; kind: "Open" | "Won" | "Lost"; defaultProbability: number }
export interface Deal { id: string; number: string; name: string; amount: number; currency: string; stageId: string; probability: number; version: number; companyName?: string }
interface Board { stages: Stage[]; deals: Deal[] }
const stop = { onPointerDown: (e: React.SyntheticEvent) => e.stopPropagation(), onTouchStart: (e: React.SyntheticEvent) => e.stopPropagation(), onKeyDown: (e: React.SyntheticEvent) => e.stopPropagation() };

function Card({ deal, lang, stages, onMove, onOpen, overlay = false }: { deal: Deal; lang: string; stages: Stage[]; onMove?: (d: Deal, stageId: string) => void; onOpen?: (d: Deal) => void; overlay?: boolean }) {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({ id: deal.id });
  return (
    <div ref={overlay ? undefined : setNodeRef} {...(overlay ? {} : { ...listeners, ...attributes })}
      onClick={() => onOpen?.(deal)}
      className={cn("cursor-grab touch-manipulation rounded-lg border border-slate-200 bg-card p-3 shadow-sm dark:border-slate-700 dark:bg-card-dark", isDragging && "opacity-40", overlay && "rotate-1 shadow-lg")}>
      <div className="font-mono text-xs text-slate-500" dir="ltr">{deal.number}</div>
      <div className="mt-1 text-[15px] font-medium">{deal.name}</div>
      {deal.companyName && <div className="text-xs text-slate-500">{deal.companyName}</div>}
      <div className="mt-2 flex items-center justify-between text-sm">
        <span className="tabular font-semibold text-primary">{money(deal.amount, deal.currency, lang)}</span>
        <span className="tabular text-slate-500">{deal.probability}%</span>
      </div>
      {onMove && !overlay && (
        <select {...stop} aria-label={t("moveTo")} value={deal.stageId} onChange={e => onMove(deal, e.target.value)} className="input mt-2 md:hidden">
          {stages.map(s => <option key={s.id} value={s.id}>{lang === "ar" ? s.nameAr : s.name}</option>)}
        </select>
      )}
    </div>
  );
}

function Column({ stage, deals, lang, stages, onMove, onOpen }: { stage: Stage; deals: Deal[]; lang: string; stages: Stage[]; onMove: (d: Deal, s: string) => void; onOpen: (d: Deal) => void }) {
  const { setNodeRef, isOver } = useDroppable({ id: stage.id });
  const sum = deals.reduce((s, d) => s + d.amount, 0);
  return (
    <section ref={setNodeRef} id={`col-${stage.id}`} aria-label={lang === "ar" ? stage.nameAr : stage.name}
      className={cn("flex w-[85vw] shrink-0 snap-center flex-col rounded-xl bg-[#EEF3F5] p-2 dark:bg-slate-900 sm:w-[22rem] md:w-72 xl:w-80 2xl:w-auto 2xl:min-w-72 2xl:flex-1", isOver && "ring-2 ring-primary")}>
      <header className="flex items-center justify-between px-2 py-2">
        <h3 className="text-sm font-semibold">{lang === "ar" ? stage.nameAr : stage.name} <span className="text-slate-500">({deals.length})</span></h3>
        <span className="tabular text-xs text-slate-500">{money(sum, deals[0]?.currency ?? "SAR", lang)}</span>
      </header>
      <div className="flex min-h-24 flex-col gap-2">{deals.map(d => <Card key={d.id} deal={d} lang={lang} stages={stages} onMove={onMove} onOpen={onOpen} />)}</div>
    </section>
  );
}

export function DealsBoard() {
  const { t, i18n } = useTranslation(); const qc = useQueryClient(); const lang = i18n.language;
  const [active, setActive] = useState<Deal | null>(null);
  const [lostFor, setLostFor] = useState<{ deal: Deal; stageId: string } | null>(null);
  const [detail, setDetail] = useState<Deal | null>(null);
  const [reason, setReason] = useState(""); const [tab, setTab] = useState(0);
  const { data } = useQuery({ queryKey: ["board"], queryFn: async () => (await api.get<Board>("/deals/board")).data });

  const move = useMutation({
    mutationFn: (v: { deal: Deal; stageId: string; lost?: { reason: string } }) => api.post(`/deals/${v.deal.id}/move-stage`, { stageId: v.stageId, version: v.deal.version, lost: v.lost }),
    onMutate: async ({ deal, stageId }) => {
      await qc.cancelQueries({ queryKey: ["board"] });
      const prev = qc.getQueryData<Board>(["board"]);
      qc.setQueryData<Board>(["board"], b => b && { ...b, deals: b.deals.map(d => d.id === deal.id ? { ...d, stageId } : d) });
      return { prev };
    },
    onError: (err, _v, ctx) => {
      if (ctx?.prev) qc.setQueryData(["board"], ctx.prev);
      const status = (err as AxiosError).response?.status;
      if (status === 409) toast.info(t("conflict")); else toast.error(problem(err));
    },
    onSettled: () => qc.invalidateQueries({ queryKey: ["board"] }),
  });

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  if (!data) return <div className="flex gap-3 overflow-hidden">{[...Array(4)].map((_, i) => <div key={i} className="h-64 w-72 shrink-0 animate-pulse rounded-xl bg-slate-200 dark:bg-slate-800" />)}</div>;
  const stages = [...data.stages].sort((a, b) => a.position - b.position);

  const request = (deal: Deal, stageId: string) => {
    if (stageId === deal.stageId) return;
    const stage = stages.find(s => s.id === stageId);
    if (stage?.kind === "Lost") { setLostFor({ deal, stageId }); return; }
    move.mutate({ deal, stageId });
  };
  const onStart = (e: DragStartEvent) => setActive(data.deals.find(d => d.id === e.active.id) ?? null);
  const onEnd = (e: DragEndEvent) => {
    setActive(null);
    const deal = data.deals.find(d => d.id === e.active.id);
    if (deal && e.over) request(deal, e.over.id as string);
  };
  const jump = (i: number) => { setTab(i); document.getElementById(`col-${stages[i].id}`)?.scrollIntoView({ behavior: "smooth", inline: "center", block: "nearest" }); };

  return (
    <>
      {/* Phone stage tabs */}
      <div className="-mx-3 mb-2 flex gap-2 overflow-x-auto px-3 pb-1 md:hidden" role="tablist">
        {stages.map((s, i) => (
          <button key={s.id} role="tab" aria-selected={tab === i} onClick={() => jump(i)}
            className={cn("min-h-[44px] shrink-0 rounded-full border px-4 text-sm", tab === i ? "border-primary bg-primary text-white" : "border-slate-300 dark:border-slate-700")}>
            {lang === "ar" ? s.nameAr : s.name} · {data.deals.filter(d => d.stageId === s.id).length}
          </button>
        ))}
      </div>
      <DndContext sensors={sensors} collisionDetection={closestCorners} onDragStart={onStart} onDragEnd={onEnd}>
        <div className="-mx-3 flex snap-x snap-mandatory gap-3 overflow-x-auto px-3 pb-3 sm:-mx-4 sm:px-4 md:mx-0 md:snap-none md:px-0"
          onScroll={e => { const el = e.currentTarget; const i = Math.round(Math.abs(el.scrollLeft) / (el.scrollWidth / stages.length)); if (i !== tab && i < stages.length) setTab(i); }}>
          {stages.map(s => <Column key={s.id} stage={s} lang={lang} stages={stages} onMove={request} onOpen={setDetail} deals={data.deals.filter(d => d.stageId === s.id)} />)}
        </div>
        <DragOverlay>{active && <Card deal={active} lang={lang} stages={stages} overlay />}</DragOverlay>
      </DndContext>

      <DealDetails deal={detail} lang={lang} onClose={() => setDetail(null)} />
      <Sheet open={!!lostFor} onClose={() => { setLostFor(null); setReason(""); }} title={t("lostReason")}>
        <textarea aria-label={t("lostReason")} value={reason} onChange={e => setReason(e.target.value)} className="input h-24 py-2" />
        <div className="mt-4 flex justify-end gap-2">
          <button className="btn-ghost" onClick={() => { setLostFor(null); setReason(""); }}>{t("cancel")}</button>
          <button className="btn-primary" disabled={!reason.trim()} onClick={() => { if (lostFor) move.mutate({ ...lostFor, lost: { reason } }); setLostFor(null); setReason(""); }}>{t("confirm")}</button>
        </div>
      </Sheet>
    </>
  );
}

interface Log { id: number; action: string; description: string; actorName?: string; timestamp: string }
function DealDetails({ deal, lang, onClose }: { deal: Deal | null; lang: string; onClose(): void }) {
  const { t } = useTranslation();
  const { data = [], isLoading } = useQuery({ queryKey: ["deal-activities", deal?.id], enabled: !!deal, queryFn: async () => (await api.get<Log[]>(`/deals/${deal!.id}/activities`)).data });
  return (
    <Sheet open={!!deal} onClose={onClose} title={deal?.name ?? ""}>
      {deal && <div className="mb-4 flex items-center justify-between text-sm"><span className="font-mono text-slate-500" dir="ltr">{deal.number}</span><span className="tabular font-semibold text-primary">{money(deal.amount, deal.currency, lang)}</span></div>}
      <h3 className="mb-2 text-sm font-semibold">{t("timeline")}</h3>
      {isLoading ? <div className="h-16 animate-pulse rounded-lg bg-slate-200 dark:bg-slate-800" /> : data.length === 0 ? <p className="text-sm text-slate-500">{t("noData")}</p> : (
        <ol className="relative space-y-4 border-s border-slate-300 ps-4 dark:border-slate-700">
          {data.map(l => (
            <li key={l.id} className="relative">
              <span className="absolute -start-[1.3rem] top-1.5 h-2.5 w-2.5 rounded-full bg-primary" />
              <div className="text-sm">{l.description}</div>
              <div className="text-xs text-slate-500">{l.actorName ? `${t("by")} ${l.actorName} · ` : ""}{new Date(l.timestamp).toLocaleString(lang === "ar" ? "ar-SA" : "en-GB")}</div>
            </li>
          ))}
        </ol>
      )}
    </Sheet>
  );
}
