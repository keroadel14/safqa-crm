import { useEffect, type ReactNode } from "react";
import { cn } from "../lib/cn";

/** Slide-over navigation drawer for phones. Opens from the inline-start edge (left in LTR, right in RTL). */
export function Drawer({ open, onClose, title, children }: { open: boolean; onClose(): void; title: string; children: ReactNode }) {
  useEffect(() => {
    if (!open) return;
    const h = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", h);
    const prev = document.body.style.overflow; document.body.style.overflow = "hidden";   // no background scroll while open
    return () => { window.removeEventListener("keydown", h); document.body.style.overflow = prev; };
  }, [open, onClose]);
  return (
    <div className={cn("fixed inset-0 z-50 md:hidden", !open && "pointer-events-none")} aria-hidden={!open}>
      <div onClick={onClose} className={cn("absolute inset-0 bg-black/50 transition-opacity duration-200", open ? "opacity-100" : "opacity-0")} />
      <aside role="dialog" aria-modal="true" aria-label={title}
        className={cn("absolute inset-y-0 start-0 flex w-72 max-w-[85vw] flex-col bg-secondary pt-[env(safe-area-inset-top)] text-slate-200 shadow-xl transition-transform duration-200 ease-out",
          open ? "translate-x-0" : "-translate-x-full rtl:translate-x-full")}>
        {children}
      </aside>
    </div>
  );
}
