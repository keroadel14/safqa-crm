import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
type Theme = "light" | "dark";
type Lang = "ar" | "en";
const Ctx = createContext<{ theme: Theme; lang: Lang; toggleTheme(): void; setLang(l: Lang): void } | null>(null);
export const useShell = () => { const c = useContext(Ctx); if (!c) throw new Error("ShellProvider missing"); return c; };

export function ShellProvider({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation();
  const [theme, setTheme] = useState<Theme>(() => (localStorage.getItem("theme") as Theme) ??
    (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"));
  const [lang, setLangState] = useState<Lang>(() => (localStorage.getItem("lang") as Lang) ?? "ar");
  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
    localStorage.setItem("theme", theme);
  }, [theme]);
  useEffect(() => {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";   // flips every logical Tailwind utility (ms-/me-/ps-/pe-)
    localStorage.setItem("lang", lang);
    void i18n.changeLanguage(lang);
  }, [lang, i18n]);
  return <Ctx.Provider value={{ theme, lang, toggleTheme: () => setTheme(t => t === "dark" ? "light" : "dark"), setLang: setLangState }}>{children}</Ctx.Provider>;
}
