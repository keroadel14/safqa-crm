import { createContext, useContext, useState, type ReactNode } from "react";
import { api, tokens } from "./api";
export interface User { id: string; email: string; fullName: string; role: "Admin" | "Manager" | "Rep"; organizationId: string }
const Ctx = createContext<{ user: User | null; login(e: string, p: string): Promise<void>; logout(): void } | null>(null);
export const useAuth = () => { const c = useContext(Ctx); if (!c) throw new Error("AuthProvider missing"); return c; };

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => { try { return tokens.access ? JSON.parse(localStorage.getItem("user") ?? "null") : null; } catch { return null; } });
  const login = async (email: string, password: string) => {
    const { data } = await api.post("/auth/login", { email, password });
    tokens.set(data.token, data.refreshToken); localStorage.setItem("user", JSON.stringify(data.user)); setUser(data.user);
  };
  const logout = () => { tokens.clear(); setUser(null); };
  return <Ctx.Provider value={{ user, login, logout }}>{children}</Ctx.Provider>;
}
