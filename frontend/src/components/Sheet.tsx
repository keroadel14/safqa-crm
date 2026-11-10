import { X } from "lucide-react";
import { useEffect, useRef, type ReactNode } from "react";
import { cn } from "../lib/cn";

/** Modal on sm+, bottom sheet (drawer) on phones. */
export function Sheet({ open, onClose, title, children, className }: { open: boolean; onClose(): void; title: string; children: ReactNode; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const h = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", h); ref.current?.focus();
    return () => window.removeEventListener("keydown", h);
  }, [open, onClose]);
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onMouseDown={e => e.target === e.currentTarget && onClose()}>
      <div ref={ref} tabIndex={-1} role="dialog" aria-modal="true" aria-label={title}
        className={cn("max-h-[90dvh] w-full overflow-y-auto overscroll-contain rounded-t-2xl bg-card p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom))] shadow-xl outline-none dark:bg-card-dark sm:max-w-lg sm:rounded-xl sm:pb-5", className)}>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button onClick={onClose} aria-label="Close" className="inline-flex h-11 w-11 items-center justify-center rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800"><X size={20} /></button>
        </div>
        {children}
      </div>
    </div>
  );
}
