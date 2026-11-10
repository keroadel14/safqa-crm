import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { DealsBoard } from "../features/deals/DealsBoard";
import { downloadFile, problem } from "../lib/api";
import { useAuth } from "../lib/auth";

export default function DealsPage() {
  const { t } = useTranslation(); const { user } = useAuth();
  return (
    <div>
      <div className="mb-3 flex items-center justify-between gap-2">
        <h1 className="text-xl font-semibold md:text-2xl">{t("deals")}</h1>
        {user?.role !== "Rep" && <button className="btn-ghost" onClick={() => void downloadFile("/deals/export", "deals.csv").catch(e => toast.error(problem(e)))}>{t("exportCsv")}</button>}
      </div>
      <DealsBoard />
    </div>
  );
}
