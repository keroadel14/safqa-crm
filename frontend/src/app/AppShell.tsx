import { Bell, Building2, ChevronsLeft, ChevronsRight, FileText, Handshake, LayoutDashboard, Languages, LogOut, Menu, Moon, Plus, Search, Settings, Sun, Users } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, NavLink, Outlet, useNavigate } from "react-router-dom";
import { CommandPalette } from "../components/CommandPalette";
import { Sheet } from "../components/Sheet";
import { cn } from "../lib/cn";
import { useAuth } from "../lib/auth";
import { useNotifications } from "../lib/notifications";
import { ShellSwitcher } from "./ShellSwitcher";
import { useShell } from "./ThemeLangProvider";
import { Drawer } from "../components/Drawer";

const NAV = [
  { to: "/dashboard", icon: LayoutDashboard, key: "dashboard" },
  { to: "/leads", icon: Users, key: "leads" },
  { to: "/deals", icon: Handshake, key: "deals" },
  { to: "/quotes", icon: FileText, key: "quotes" },
  { to: "/settings", icon: Settings, key: "settings", admin: true },
];

export function AppShell() {
  const { t } = useTranslation(); const { user, logout } = useAuth(); const nav = useNavigate();
  const { notes, markRead, status } = useNotifications();
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem("sidebar") === "1");
  const [menu, setMenu] = useState(false); const { theme, lang, toggleTheme, setLang } = useShell();
  const [palette, setPalette] = useState(false); const [bell, setBell] = useState(false); const [quick, setQuick] = useState(false);
  const items = NAV.filter(n => !n.admin || user?.role === "Admin");
  const unread = notes.filter(n => !n.read).length;

  useEffect(() => {
    const h = (e: KeyboardEvent) => { if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") { e.preventDefault(); setPalette(p => !p); } };
    window.addEventListener("keydown", h); return () => window.removeEventListener("keydown", h);
  }, []);
  const toggle = () => setCollapsed(c => { localStorage.setItem("sidebar", c ? "0" : "1"); return !c; });
  const iconBtn = "relative inline-flex h-11 w-11 items-center justify-center rounded-lg border border-slate-200 hover:bg-slate-100 dark:border-slate-700 dark:hover:bg-slate-800 md:h-9 md:w-9";

  return (
    <div className="flex min-h-dvh">
      {/* Sidebar: hidden on phones, icon rail on tablets, full on laptops (user-collapsible) */}
      <aside className={cn("sticky top-0 hidden h-dvh shrink-0 flex-col border-e border-slate-200 bg-secondary text-slate-200 md:flex dark:border-slate-800", collapsed ? "w-16" : "w-16 lg:w-60")}>
        <Link to="/dashboard" className="flex h-14 items-center gap-2 px-4 text-lg font-bold text-white">
          <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-primary">ص</span>
          <span className={cn("hidden", !collapsed && "lg:inline")}>{t("appName")}</span>
        </Link>
        <nav className="flex-1 space-y-1 px-2 py-2">
          {items.map(({ to, icon: Icon, key }) => (
            <NavLink key={to} to={to} className={({ isActive }) => cn("group relative flex h-11 items-center gap-3 rounded-lg px-3 hover:bg-white/10", isActive && "bg-primary text-white")}>
              <Icon size={20} className="shrink-0" />
              <span className={cn("hidden", !collapsed && "lg:inline")}>{t(key)}</span>
              <span role="tooltip" className={cn("pointer-events-none absolute start-full z-50 ms-2 whitespace-nowrap rounded bg-slate-900 px-2 py-1 text-xs text-white opacity-0 shadow group-hover:opacity-100", !collapsed && "lg:hidden")}>{t(key)}</span>
            </NavLink>
          ))}
        </nav>
        <button onClick={toggle} className="m-2 hidden h-10 items-center justify-center rounded-lg hover:bg-white/10 lg:flex" aria-label="Toggle sidebar">
          {collapsed ? <ChevronsRight size={18} className="rtl:rotate-180" /> : <ChevronsLeft size={18} className="rtl:rotate-180" />}
        </button>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-slate-200 bg-card/90 px-3 backdrop-blur dark:border-slate-800 dark:bg-card-dark/90 md:px-5">
          <button className={cn(iconBtn, "md:hidden")} onClick={() => setMenu(true)} aria-label={t("menu")} aria-expanded={menu}><Menu size={20} /></button>
          <button disabled title={t("org")} className="hidden h-9 items-center gap-2 rounded-lg border border-slate-200 px-3 text-sm dark:border-slate-700 sm:inline-flex"><Building2 size={16} />{t("appName")}</button>
          <Link to="/dashboard" className="hidden h-9 w-9 place-items-center rounded-lg bg-primary font-bold text-white min-[400px]:grid sm:hidden">ص</Link>
          <button onClick={() => setPalette(true)} className="mx-2 flex h-11 min-w-0 flex-1 items-center gap-2 rounded-lg border border-slate-200 px-3 text-start text-sm text-slate-500 dark:border-slate-700 md:h-9 md:max-w-md">
            <Search size={16} /><span className="truncate">{t("search")}</span><kbd className="ms-auto hidden rounded border px-1.5 text-xs lg:inline">Ctrl K</kbd>
          </button>
          <div className="relative">
            <button className={iconBtn} aria-label={t("notifications")} onClick={() => { setBell(b => !b); if (!bell) markRead(); }}>
              <Bell size={18} />{status !== "connected" && <span title={t("reconnecting")} className="absolute -start-0.5 -top-0.5 h-2.5 w-2.5 rounded-full bg-amber-500" />}{unread > 0 && <span className="absolute -end-1 -top-1 grid h-4 min-w-4 place-items-center rounded-full bg-accent px-1 text-[10px] text-white">{unread}</span>}
            </button>
            {bell && (
              <div className="absolute end-0 mt-2 w-[min(20rem,calc(100vw-1.5rem))] rounded-xl border border-slate-200 bg-card p-2 shadow-lg dark:border-slate-700 dark:bg-card-dark">
                {notes.length === 0 ? <p className="p-3 text-sm text-slate-500">{t("noNotes")}</p> :
                  <ul className="max-h-80 overflow-y-auto">{notes.map(n => <li key={n.id} className="rounded-lg p-2 text-sm hover:bg-slate-100 dark:hover:bg-slate-800">{n.text}<div className="text-xs text-slate-500">{n.at.toLocaleTimeString()}</div></li>)}</ul>}
              </div>
            )}
          </div>
          <div className="hidden md:block"><ShellSwitcher /></div>
          <button className={cn(iconBtn, "hidden md:inline-flex")} onClick={() => { logout(); nav("/login"); }} aria-label={t("logout")} title={user?.fullName}><LogOut size={18} /></button>
        </header>

        <main className="mx-auto w-full max-w-screen-2xl flex-1 p-3 pb-24 sm:p-4 md:p-6 md:pb-6 3xl:max-w-[1800px]"><Outlet /></main>
      </div>

      {/* Phone bottom navigation */}
      <nav className="fixed inset-x-0 bottom-0 z-40 grid grid-cols-5 items-center border-t border-slate-200 bg-card pb-[env(safe-area-inset-bottom)] dark:border-slate-800 dark:bg-card-dark md:hidden" aria-label="Primary">
        {items.slice(0, 2).map(({ to, icon: Icon, key }) => <BottomLink key={to} to={to} Icon={Icon} label={t(key)} />)}
        <button onClick={() => setQuick(true)} aria-label={t("quickActions")} className="mx-auto -mt-5 grid h-14 w-14 place-items-center rounded-full bg-accent text-white shadow-lg"><Plus size={26} /></button>
        {items.slice(2, 4).map(({ to, icon: Icon, key }) => <BottomLink key={to} to={to} Icon={Icon} label={t(key)} />)}
      </nav>
      <Sheet open={quick} onClose={() => setQuick(false)} title={t("quickActions")}>
        <div className="grid gap-2">
          <Link onClick={() => setQuick(false)} to="/leads?new=1" className="btn-primary">{t("newLead")}</Link>
          <Link onClick={() => setQuick(false)} to="/quotes" className="btn-ghost">{t("quoteBuilder")}</Link>
          <Link onClick={() => setQuick(false)} to="/deals" className="btn-ghost">{t("deals")}</Link>
        </div>
      </Sheet>
      <Drawer open={menu} onClose={() => setMenu(false)} title={t("menu")}>
        <div className="flex items-center gap-3 border-b border-white/10 p-4">
          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-primary font-bold text-white">{user?.fullName?.[0] ?? "؟"}</span>
          <div className="min-w-0"><div className="truncate font-medium text-white">{user?.fullName}</div><div className="truncate text-xs text-slate-400" dir="ltr">{user?.email} · {user?.role}</div></div>
        </div>
        <nav className="flex-1 space-y-1 overflow-y-auto p-2">
          {items.map(({ to, icon: Icon, key }) => (
            <NavLink key={to} to={to} onClick={() => setMenu(false)} className={({ isActive }) => cn("flex min-h-[48px] items-center gap-3 rounded-lg px-3 hover:bg-white/10", isActive && "bg-primary text-white")}><Icon size={20} />{t(key)}</NavLink>
          ))}
        </nav>
        <div className="space-y-2 border-t border-white/10 p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))]">
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => setLang(lang === "ar" ? "en" : "ar")} className="flex min-h-[44px] items-center justify-center gap-2 rounded-lg border border-white/20 hover:bg-white/10"><Languages size={18} />{lang === "ar" ? "English" : "العربية"}</button>
            <button onClick={toggleTheme} className="flex min-h-[44px] items-center justify-center gap-2 rounded-lg border border-white/20 hover:bg-white/10">{theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}{t("theme")}</button>
          </div>
          <button onClick={() => { setMenu(false); logout(); nav("/login"); }} className="flex min-h-[44px] w-full items-center justify-center gap-2 rounded-lg bg-white/10 hover:bg-white/20"><LogOut size={18} />{t("logout")}</button>
        </div>
      </Drawer>
      <CommandPalette open={palette} onClose={() => setPalette(false)} />
    </div>
  );
}

function BottomLink({ to, Icon, label }: { to: string; Icon: typeof Users; label: string }) {
  return (
    <NavLink to={to} className={({ isActive }) => cn("flex min-h-[56px] flex-col items-center justify-center gap-0.5 text-[11px] text-slate-500", isActive && "text-primary")}>
      <Icon size={22} /><span className="max-w-full truncate px-1">{label}</span>
    </NavLink>
  );
}
