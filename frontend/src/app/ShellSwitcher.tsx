import { Moon, Sun, Languages } from "lucide-react";
import { useShell } from "./ThemeLangProvider";
export function ShellSwitcher() {
  const { theme, lang, toggleTheme, setLang } = useShell();
  const btn = "inline-flex h-9 w-9 items-center justify-center rounded-md border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 focus-visible:outline-2 focus-visible:outline-[#0E7490]";
  return (
    <div className="flex items-center gap-2">
      <button className={btn} onClick={toggleTheme} aria-label={theme === "dark" ? "Light mode" : "Dark mode"}>
        {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
      </button>
      <button className={btn} onClick={() => setLang(lang === "ar" ? "en" : "ar")} aria-label={lang === "ar" ? "English" : "العربية"}>
        <Languages size={18} />
      </button>
    </div>
  );
}
