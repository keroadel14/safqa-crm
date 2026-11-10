import i18n from "i18next";
import { initReactI18next } from "react-i18next";
const en = {
  appName: "Safqa", dashboard: "Dashboard", leads: "Leads", deals: "Deals", quotes: "Quotes", settings: "Settings",
  board: "Deals", won: "Won", lost: "Lost", total: "Total", conflict: "This deal was changed by someone else. Reloaded.",
  lostReason: "Reason for loss", confirm: "Confirm", cancel: "Cancel", save: "Save", loading: "Loading…", search: "Search…",
  logout: "Sign out", newLead: "New lead", convert: "Convert", moveTo: "Move to stage", quickActions: "Quick actions",
  email: "Email", password: "Password", signIn: "Sign in", notifications: "Notifications", noResults: "No results", noNotes: "No notifications",
  overdue: "Overdue tasks", meetings: "Today's meetings", uncontacted: "Uncontacted leads", breached: "older than 4h", stalled: "Stalled deals",
  forecast: "Weighted forecast (next 6 months)", weighted: "Weighted pipeline", pipeline: "Open pipeline",
  firstName: "First name", lastName: "Last name", phone: "Phone (+966…)", company: "Company", source: "Source", status: "Status",
  quoteBuilder: "Quote builder", deal: "Deal", validUntil: "Valid until", terms: "Terms", addLine: "Add line", product: "Product / description",
  qty: "Qty", price: "Price", discount: "Disc %", vat: "VAT %", subtotal: "Subtotal", discountTotal: "Discount", vatTotal: "VAT", grandTotal: "Total",
  saveDownload: "Save & download PDF", recentQuotes: "Recent quotes", needsApproval: "Discount above your limit — sent for approval.",
  users: "Users", teams: "Teams", stages: "Stages", audit: "Audit log", name: "Name", role: "Role", active: "Active", add: "Add",
  org: "Organization", noData: "Nothing here yet", timeline: "Activity timeline", exportCsv: "Export CSV", menu: "Menu", theme: "Theme", language: "Language", reconnecting: "Reconnecting…", unqualifiedReason: "Reason for disqualification?", by: "by",
};
const ar: typeof en = {
  appName: "صفقة", dashboard: "لوحة التحكم", leads: "العملاء المحتملون", deals: "الصفقات", quotes: "عروض الأسعار", settings: "الإعدادات",
  board: "الصفقات", won: "رابحة", lost: "خاسرة", total: "الإجمالي", conflict: "تم تعديل هذه الصفقة من مستخدم آخر. تمت إعادة التحميل.",
  lostReason: "سبب الخسارة", confirm: "تأكيد", cancel: "إلغاء", save: "حفظ", loading: "جارٍ التحميل…", search: "بحث…",
  logout: "تسجيل الخروج", newLead: "عميل محتمل جديد", convert: "تحويل", moveTo: "نقل إلى مرحلة", quickActions: "إجراءات سريعة",
  email: "البريد الإلكتروني", password: "كلمة المرور", signIn: "تسجيل الدخول", notifications: "الإشعارات", noResults: "لا نتائج", noNotes: "لا إشعارات",
  overdue: "مهام متأخرة", meetings: "اجتماعات اليوم", uncontacted: "عملاء لم يُتواصل معهم", breached: "أقدم من 4 ساعات", stalled: "صفقات متوقفة",
  forecast: "التوقعات المرجّحة (6 أشهر)", weighted: "الخط المرجّح", pipeline: "الخط المفتوح",
  firstName: "الاسم الأول", lastName: "اسم العائلة", phone: "الجوال (+966…)", company: "الشركة", source: "المصدر", status: "الحالة",
  quoteBuilder: "منشئ عروض الأسعار", deal: "الصفقة", validUntil: "صالح حتى", terms: "الشروط", addLine: "إضافة بند", product: "المنتج / الوصف",
  qty: "الكمية", price: "السعر", discount: "خصم %", vat: "ضريبة %", subtotal: "المجموع الفرعي", discountTotal: "الخصم", vatTotal: "الضريبة", grandTotal: "الإجمالي",
  saveDownload: "حفظ وتنزيل PDF", recentQuotes: "أحدث العروض", needsApproval: "الخصم أعلى من حدك — أُرسل للموافقة.",
  users: "المستخدمون", teams: "الفرق", stages: "المراحل", audit: "سجل التدقيق", name: "الاسم", role: "الدور", active: "نشط", add: "إضافة",
  org: "المؤسسة", noData: "لا يوجد شيء بعد", timeline: "سجل النشاط", exportCsv: "تصدير CSV", menu: "القائمة", theme: "المظهر", language: "اللغة", reconnecting: "جارٍ إعادة الاتصال…", unqualifiedReason: "سبب عدم التأهيل؟", by: "بواسطة",
};
void i18n.use(initReactI18next).init({ resources: { en: { translation: en }, ar: { translation: ar } }, lng: localStorage.getItem("lang") ?? "ar", fallbackLng: "en", interpolation: { escapeValue: false } });
export default i18n;
