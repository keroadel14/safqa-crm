import { lazy, Suspense, type ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AppShell } from "./app/AppShell";
import { useAuth } from "./lib/auth";
const LoginPage = lazy(() => import("./pages/LoginPage"));
const DashboardPage = lazy(() => import("./pages/DashboardPage"));
const LeadsPage = lazy(() => import("./pages/LeadsPage"));
const DealsPage = lazy(() => import("./pages/DealsPage"));
const QuotesPage = lazy(() => import("./pages/QuotesPage"));
const AdminSettingsPage = lazy(() => import("./pages/AdminSettingsPage"));

function Protected({ children, admin }: { children: ReactNode; admin?: boolean }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (admin && user.role !== "Admin") return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

export default function App() {
  const { user } = useAuth();
  return (
    <Suspense fallback={<div className="p-8 text-center text-slate-500">…</div>}>
      <Routes>
        <Route path="/login" element={user ? <Navigate to="/dashboard" replace /> : <LoginPage />} />
        <Route element={<Protected><AppShell /></Protected>}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="leads" element={<LeadsPage />} />
          <Route path="deals" element={<DealsPage />} />
          <Route path="quotes" element={<QuotesPage />} />
          <Route path="settings" element={<Protected admin><AdminSettingsPage /></Protected>} />
        </Route>
        <Route path="*" element={<div className="p-10 text-center text-lg">404</div>} />
      </Routes>
    </Suspense>
  );
}
