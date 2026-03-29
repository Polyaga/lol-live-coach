import { AdminBackofficeView } from "../../components/admin/backoffice-view";
import {
  ERROR_MESSAGES,
  NOTICE_MESSAGES,
  getVisibleAdminSections,
  normalizeAdminSection
} from "../../components/admin/backoffice-helpers";
import { adminHasPermission, requireAdminSession } from "../../lib/admin-auth";
import { ADMIN_PERMISSIONS } from "../../lib/admin-rbac";
import { getAdminDashboardData } from "../../lib/admin-store";
import { getSiteContent } from "../../lib/site-config";

export default async function AdminPage({ searchParams }) {
  const params = await searchParams;
  const session = await requireAdminSession();
  const site = getSiteContent();
  const origin = process.env.SITE_URL || process.env.NEXT_PUBLIC_SITE_URL;
  const data = await getAdminDashboardData(origin);

  const capabilities = {
    canManageAccess: adminHasPermission(session, ADMIN_PERMISSIONS.ACCESS_MANAGE),
    canManageAmbassadors: adminHasPermission(session, ADMIN_PERMISSIONS.AMBASSADORS_MANAGE),
    canManageRoles: adminHasPermission(session, ADMIN_PERMISSIONS.ROLES_MANAGE),
    canManageUsers: adminHasPermission(session, ADMIN_PERMISSIONS.USERS_MANAGE),
    canViewAccess: adminHasPermission(session, ADMIN_PERMISSIONS.ACCESS_VIEW),
    canViewAmbassadors: adminHasPermission(session, ADMIN_PERMISSIONS.AMBASSADORS_VIEW),
    canViewAnalytics: adminHasPermission(session, ADMIN_PERMISSIONS.ANALYTICS_VIEW),
    canViewDashboard: adminHasPermission(session, ADMIN_PERMISSIONS.DASHBOARD_VIEW),
    canViewRoles: adminHasPermission(session, ADMIN_PERMISSIONS.ROLES_VIEW),
    canViewUsers: adminHasPermission(session, ADMIN_PERMISSIONS.USERS_VIEW)
  };
  const visibleSections = getVisibleAdminSections({
    capabilities,
    data
  });
  const activeSection = normalizeAdminSection(params?.section, visibleSections);
  const searchQuery = String(params?.q || "").trim();

  const notice = NOTICE_MESSAGES[String(params?.notice || "")] || null;
  const error = ERROR_MESSAGES[String(params?.error || "")] || null;

  return (
    <AdminBackofficeView
      activeSection={activeSection}
      capabilities={capabilities}
      data={data}
      error={error}
      notice={notice}
      searchQuery={searchQuery}
      session={session}
      site={site}
    />
  );
}
