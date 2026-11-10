import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { api, problem } from "../lib/api";
import { useAuth } from "../lib/auth";
import { ShellSwitcher } from "../app/ShellSwitcher";

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const nav = useNavigate();

  const [mode, setMode] = useState<"login" | "reset">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [err, setErr] = useState("");
  const [msg, setMsg] = useState("");
  const [busy, setBusy] = useState(false);

  const submitLogin = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr("");
    try {
      await login(email, password);
      nav("/dashboard");
    } catch (x) {
      setErr(problem(x));
    } finally {
      setBusy(false);
    }
  };

  const submitReset = async (e: FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      setErr(t("passwordsDoNotMatch", "كلمتا المرور غير متطابقتين"));
      return;
    }
    setBusy(true);
    setErr("");
    setMsg("");
    try {
      await api.post("/auth/reset-password", { email, newPassword });
      setMsg(t("resetSuccess", "تم تغيير كلمة المرور بنجاح! يمكنك الآن تسجيل الدخول."));
      setPassword("");
      setTimeout(() => {
        setMode("login");
        setMsg("");
      }, 2500);
    } catch (x) {
      setErr(problem(x));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid min-h-dvh place-items-center p-4">
      <div className="absolute end-4 top-4"><ShellSwitcher /></div>
      
      {mode === "login" ? (
        <form onSubmit={submitLogin} className="card w-full max-w-sm space-y-4">
          <div className="text-center">
            <div className="mx-auto mb-2 grid h-12 w-12 place-items-center rounded-xl bg-primary text-xl font-bold text-white">ص</div>
            <h1 className="text-xl font-semibold">{t("appName", "صفقة CRM")}</h1>
          </div>
          
          <label className="block text-sm">
            {t("email", "البريد الإلكتروني")}
            <input className="input mt-1" type="email" autoComplete="username" required value={email} onChange={e => setEmail(e.target.value)} dir="ltr" />
          </label>
          
          <label className="block text-sm">
            <div className="flex justify-between items-center">
              <span>{t("password", "كلمة المرور")}</span>
              <button 
                type="button" 
                onClick={() => { setMode("reset"); setErr(""); setMsg(""); }} 
                className="text-xs text-primary hover:underline cursor-pointer"
              >
                {t("forgotPassword", "نسيت كلمة المرور؟")}
              </button>
            </div>
            <input className="input mt-1" type="password" autoComplete="current-password" required value={password} onChange={e => setPassword(e.target.value)} dir="ltr" />
          </label>

          {err && <p role="alert" className="text-sm text-red-600">{err}</p>}
          <button className="btn-primary w-full" disabled={busy}>{busy ? t("loading", "جاري التحميل...") : t("signIn", "تسجيل الدخول")}</button>
        </form>
      ) : (
        <form onSubmit={submitReset} className="card w-full max-w-sm space-y-4">
          <div className="text-center">
            <div className="mx-auto mb-2 grid h-12 w-12 place-items-center rounded-xl bg-primary text-xl font-bold text-white">ص</div>
            <h1 className="text-xl font-semibold">{t("resetPassword", "استعادة كلمة المرور")}</h1>
          </div>

          <label className="block text-sm">
            {t("email", "البريد الإلكتروني")}
            <input className="input mt-1" type="email" required value={email} onChange={e => setEmail(e.target.value)} dir="ltr" />
          </label>

          <label className="block text-sm">
            {t("newPassword", "كلمة المرور الجديدة")}
            <input className="input mt-1" type="password" required minLength={8} value={newPassword} onChange={e => setNewPassword(e.target.value)} dir="ltr" />
          </label>

          <label className="block text-sm">
            {t("confirmPassword", "تأكيد كلمة المرور")}
            <input className="input mt-1" type="password" required minLength={8} value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} dir="ltr" />
          </label>

          {err && <p role="alert" className="text-sm text-red-600">{err}</p>}
          {msg && <p role="status" className="text-sm text-green-600">{msg}</p>}

          <button className="btn-primary w-full" disabled={busy}>{busy ? t("loading", "جاري الحفظ...") : t("confirm", "تأكيد التغيير")}</button>
          
          <button 
            type="button" 
            onClick={() => { setMode("login"); setErr(""); setMsg(""); }} 
            className="w-full text-center text-sm text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
          >
            {t("backToLogin", "العودة لتسجيل الدخول")}
          </button>
        </form>
      )}
    </div>
  );
}
