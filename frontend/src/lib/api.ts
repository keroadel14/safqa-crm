import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";
export const tokens = {
  get access() { return localStorage.getItem("access"); },
  get refresh() { return localStorage.getItem("refresh"); },
  set(a: string, r: string) { localStorage.setItem("access", a); localStorage.setItem("refresh", r); },
  clear() { localStorage.removeItem("access"); localStorage.removeItem("refresh"); localStorage.removeItem("user"); },
};
export const api = axios.create({ baseURL: "/api/v1" });
api.interceptors.request.use(c => { const t = tokens.access; if (t) c.headers.Authorization = `Bearer ${t}`; return c; });

let refreshing: Promise<string> | null = null;
api.interceptors.response.use(r => r, async (err: AxiosError) => {
  const orig = err.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;
  if (err.response?.status === 401 && orig && !orig._retry && tokens.refresh && !orig.url?.includes("/auth/")) {
    orig._retry = true;
    try {
      refreshing ??= axios.post("/api/v1/auth/refresh", { refreshToken: tokens.refresh })
        .then(r => { tokens.set(r.data.token, r.data.refreshToken); return r.data.token as string; })
        .finally(() => { refreshing = null; });
      const t = await refreshing;
      orig.headers.Authorization = `Bearer ${t}`;
      return api(orig);
    } catch { tokens.clear(); location.assign("/login"); }
  }
  return Promise.reject(err);
});
export const problem = (e: unknown) => {
  const d = (e as AxiosError<{ detail?: string; title?: string }>).response?.data;
  return d?.detail ?? d?.title ?? (e as Error).message;
};

export async function downloadFile(path: string, filename: string) {
  const r = await api.get(path, { responseType: "blob" });
  const url = URL.createObjectURL(r.data as Blob);
  const a = document.createElement("a"); a.href = url; a.download = filename; a.click(); URL.revokeObjectURL(url);
}
