import { redirect } from "next/navigation";
import { getCurrentUser } from "./auth";
import {
  ensureAdminUserRoleAssignment,
  getUserAdminPermissions,
  hasAdminAccess,
  userHasPermission
} from "./admin-rbac";
import { prisma } from "./prisma";

async function hydrateAdminUser(user) {
  if (!user) {
    return null;
  }

  const normalizedUser = user.role === "ADMIN" && !user.adminRoleId
    ? await ensureAdminUserRoleAssignment(user.id)
    : user;

  if (!normalizedUser) {
    return null;
  }

  if (normalizedUser.adminRole) {
    return normalizedUser;
  }

  return prisma.user.findUnique({
    where: {
      id: normalizedUser.id
    },
    include: {
      adminRole: true
    }
  });
}

export async function getAdminSession() {
  const user = await getCurrentUser();
  const adminUser = await hydrateAdminUser(user);

  if (!adminUser || !hasAdminAccess(adminUser)) {
    return null;
  }

  return {
    user: adminUser,
    permissions: getUserAdminPermissions(adminUser)
  };
}

export async function requireAdminSession(permission) {
  const currentUser = await getCurrentUser();

  if (!currentUser) {
    redirect("/admin/login");
  }

  const session = await getAdminSession();

  if (!session) {
    redirect("/compte?admin=forbidden");
  }

  if (permission && !userHasPermission(session.user, permission)) {
    redirect("/admin?error=forbidden");
  }

  return session;
}

export function adminHasPermission(sessionOrUser, permission) {
  const user = sessionOrUser?.user || sessionOrUser;
  return userHasPermission(user, permission);
}
