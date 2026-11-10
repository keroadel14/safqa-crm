import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";
export const cn = (...i: ClassValue[]) => twMerge(clsx(i));
export const money = (n: number, cur: string, lang: string) =>
  new Intl.NumberFormat(lang === "ar" ? "ar-SA" : "en-US", { style: "currency", currency: cur, maximumFractionDigits: 2 }).format(n);
