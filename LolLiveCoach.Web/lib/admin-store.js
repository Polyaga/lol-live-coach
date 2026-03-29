import { Prisma } from "@prisma/client";
import { buildAccountAccess } from "./account-access";
import {
  ADMIN_PERMISSION_GROUPS,
  ensureDefaultAdminRoles,
  getAllAdminPermissionKeys,
  normalizePermissions,
  sanitizeRoleKey
} from "./admin-rbac";
import {
  createAmbassadorApplication,
  getAmbassadorMetrics,
  updateAmbassadorApplication
} from "./ambassador-store";
import { prisma } from "./prisma";
import { hashPassword, normalizeEmail } from "./security";
import { attachPendingUserRecordsByEmail } from "./user-linking";

const ACTIVE_SUBSCRIPTION_STATUSES = new Set(["active", "trialing", "past_due"]);
const CHURNED_SUBSCRIPTION_STATUSES = new Set(["canceled", "unpaid", "incomplete_expired", "paused"]);
const DAY_MS = 24 * 60 * 60 * 1000;

function sanitizeText(value, fallback = "") {
  return String(value ?? fallback).trim();
}

function normalizeMoney(value, fallback = 0) {
  const parsed = Number.parseFloat(String(value ?? fallback).replace(",", "."));
  return Number.isFinite(parsed) ? Math.max(0, parsed) : fallback;
}

function normalizeCommissionRateInput(value, fallback = 0.1) {
  const parsed = Number.parseFloat(String(value ?? fallback).replace(",", "."));

  if (!Number.isFinite(parsed)) {
    return fallback;
  }

  const normalized = parsed > 1 ? parsed / 100 : parsed;
  return Math.max(0, Math.min(normalized, 1));
}

function parseDate(value) {
  const normalized = sanitizeText(value);

  if (!normalized) {
    return null;
  }

  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function asDate(value) {
  if (!value) {
    return null;
  }

  const parsed = value instanceof Date ? new Date(value.getTime()) : new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function startOfDay(value = new Date()) {
  const date = asDate(value) || new Date();
  date.setHours(0, 0, 0, 0);
  return date;
}

function addDays(value, amount) {
  const date = asDate(value) || new Date();
  date.setDate(date.getDate() + amount);
  return date;
}

function startOfMonth(value = new Date()) {
  const date = startOfDay(value);
  date.setDate(1);
  return date;
}

function addMonths(value, amount) {
  const date = startOfMonth(value);
  date.setMonth(date.getMonth() + amount);
  return date;
}

function isWithinRange(value, start, end) {
  const date = asDate(value);
  return Boolean(date && date >= start && date < end);
}

function formatDayBucketLabel(value) {
  return new Intl.DateTimeFormat("fr-FR", {
    day: "2-digit",
    month: "2-digit"
  }).format(value);
}

function formatMonthBucketLabel(value) {
  return new Intl.DateTimeFormat("fr-FR", {
    month: "short"
  }).format(value).replace(/\.$/, "");
}

function buildDelta(current, previous) {
  const difference = current - previous;
  const direction = difference > 0 ? "up" : difference < 0 ? "down" : "flat";

  return {
    current,
    previous,
    difference,
    direction,
    changeRatio: previous > 0 ? difference / previous : current > 0 ? 1 : 0
  };
}

function flattenSubscriptions(users) {
  return users.flatMap((user) =>
    user.subscriptions.map((subscription) => ({
      ...subscription,
      userId: user.id
    })));
}

function buildAnalyticsSnapshot({ users, summary }) {
  const now = new Date();
  const today = startOfDay(now);
  const tomorrow = addDays(today, 1);
  const last30Start = addDays(today, -29);
  const previous30Start = addDays(last30Start, -30);
  const subscriptions = flattenSubscriptions(users);

  const countUsersCreatedInRange = (start, end) =>
    users.filter((user) => isWithinRange(user.createdAt, start, end)).length;

  const countSubscriptionsCreatedInRange = (start, end) =>
    subscriptions.filter((subscription) => isWithinRange(subscription.createdAt, start, end)).length;

  const countChurnedSubscriptionsInRange = (start, end) =>
    subscriptions.filter((subscription) =>
      CHURNED_SUBSCRIPTION_STATUSES.has(String(subscription.stripeStatus || "").toLowerCase())
      && isWithinRange(subscription.updatedAt, start, end)).length;

  const newUsers = countUsersCreatedInRange(last30Start, tomorrow);
  const previousNewUsers = countUsersCreatedInRange(previous30Start, last30Start);
  const newSubscriptions = countSubscriptionsCreatedInRange(last30Start, tomorrow);
  const previousNewSubscriptions = countSubscriptionsCreatedInRange(previous30Start, last30Start);
  const lostSubscriptions = countChurnedSubscriptionsInRange(last30Start, tomorrow);
  const previousLostSubscriptions = countChurnedSubscriptionsInRange(previous30Start, last30Start);
  const pendingCancellations = subscriptions.filter((subscription) =>
    ACTIVE_SUBSCRIPTION_STATUSES.has(String(subscription.stripeStatus || "").toLowerCase())
    && subscription.cancelAtPeriodEnd).length;
  const accountToSubscriberRate = summary.totalUsers > 0
    ? summary.activeSubscriptions / summary.totalUsers
    : 0;
  const monthSeries = Array.from({ length: 6 }, (_, index) => {
    const start = addMonths(today, index - 5);
    const end = addMonths(start, 1);
    const monthNewUsers = countUsersCreatedInRange(start, end);
    const monthNewSubscriptions = countSubscriptionsCreatedInRange(start, end);
    const monthLostSubscriptions = countChurnedSubscriptionsInRange(start, end);

    return {
      label: formatMonthBucketLabel(start),
      newUsers: monthNewUsers,
      newSubscriptions: monthNewSubscriptions,
      lostSubscriptions: monthLostSubscriptions,
      netGrowth: monthNewSubscriptions - monthLostSubscriptions
    };
  });
  const recentFlow = Array.from({ length: 30 }, (_, index) => {
    const start = addDays(last30Start, index);
    const end = addDays(start, 1);

    return {
      label: formatDayBucketLabel(start),
      newUsers: countUsersCreatedInRange(start, end),
      newSubscriptions: countSubscriptionsCreatedInRange(start, end),
      lostSubscriptions: countChurnedSubscriptionsInRange(start, end)
    };
  });

  return {
    inferenceNote: "La perte client est estimee a partir des abonnements passes dans un statut Stripe non actif sur la periode.",
    overviewCards: [
      {
        id: "new-users",
        label: "Nouveaux comptes",
        kind: "number",
        trendMode: "up-good",
        detail: "30 derniers jours",
        ...buildDelta(newUsers, previousNewUsers)
      },
      {
        id: "new-subscriptions",
        label: "Nouveaux abonnements",
        kind: "number",
        trendMode: "up-good",
        detail: "30 derniers jours",
        ...buildDelta(newSubscriptions, previousNewSubscriptions)
      },
      {
        id: "lost-subscriptions",
        label: "Perte client",
        kind: "number",
        trendMode: "down-good",
        detail: "sorties du premium",
        ...buildDelta(lostSubscriptions, previousLostSubscriptions)
      },
      {
        id: "net-growth",
        label: "Gain client net",
        kind: "signed",
        trendMode: "up-good",
        detail: "acquisition - perte",
        ...buildDelta(
          newSubscriptions - lostSubscriptions,
          previousNewSubscriptions - previousLostSubscriptions
        )
      },
      {
        id: "active-subscriptions",
        label: "Abonnements actifs",
        kind: "number",
        trendMode: "neutral",
        detail: `${pendingCancellations} resiliation(s) planifiee(s)`,
        current: summary.activeSubscriptions,
        previous: null,
        difference: null,
        direction: "flat",
        changeRatio: 0
      },
      {
        id: "subscriber-conversion",
        label: "Conversion compte > abonne",
        kind: "percent",
        trendMode: "neutral",
        detail: `${summary.activePremiumUsers} premium actif(s)`,
        current: accountToSubscriberRate,
        previous: null,
        difference: null,
        direction: "flat",
        changeRatio: 0
      }
    ],
    monthSeries,
    recentFlow,
    totals: {
      newUsers,
      newSubscriptions,
      lostSubscriptions,
      netGrowth: newSubscriptions - lostSubscriptions,
      pendingCancellations,
      activeSubscriptions: summary.activeSubscriptions,
      accountToSubscriberRate
    }
  };
}

function isManualGrantActive(grant) {
  const now = new Date();
  const startsAt = grant.startsAt instanceof Date ? grant.startsAt : new Date(grant.startsAt);
  const endsAt = grant.endsAt ? new Date(grant.endsAt) : null;

  return !grant.revokedAt
    && startsAt <= now
    && (!endsAt || endsAt >= now);
}

function summarizeUser(user, origin) {
  const access = buildAccountAccess({
    subscriptions: user.subscriptions,
    ambassadorApplications: user.ambassadorApplications,
    accessGrants: user.accessGrants,
    origin
  });
  const activeSubscription = user.subscriptions.find((subscription) =>
    ACTIVE_SUBSCRIPTION_STATUSES.has(String(subscription.stripeStatus || "").toLowerCase()));
  const latestAmbassador = user.ambassadorApplications[0] || null;

  return {
    id: user.id,
    email: user.email,
    displayName: user.displayName,
    createdAt: user.createdAt,
    role: user.role,
    adminRole: user.adminRole,
    access,
    activeSubscriptionStatus: activeSubscription?.stripeStatus || null,
    ambassadorStatus: latestAmbassador?.status || null,
    ambassadorAccessGranted: Boolean(latestAmbassador?.accessGranted),
    desktopSessionCount: user.desktopTokens.length,
    subscriptionCount: user.subscriptions.length,
    activeGrantCount: user.accessGrants.filter(isManualGrantActive).length
  };
}

async function recordAdminActivity(actorUserId, action, entityType, summary, details = "", entityId = null) {
  await prisma.adminActivityLog.create({
    data: {
      actorUserId: actorUserId || null,
      action,
      entityType,
      entityId,
      summary,
      details: sanitizeText(details) || null
    }
  }).catch(() => null);
}

async function resolveAdminRoleId(adminRoleId) {
  const normalizedRoleId = sanitizeText(adminRoleId);

  if (!normalizedRoleId) {
    return null;
  }

  const role = await prisma.adminRole.findUnique({
    where: {
      id: normalizedRoleId
    },
    select: {
      id: true
    }
  });

  return role?.id || null;
}

async function resolveUserByEmailOrId({ userId, email }) {
  const normalizedUserId = sanitizeText(userId);
  const normalizedEmail = normalizeEmail(email);

  if (normalizedUserId) {
    return prisma.user.findUnique({
      where: {
        id: normalizedUserId
      }
    });
  }

  if (!normalizedEmail) {
    return null;
  }

  return prisma.user.findUnique({
    where: {
      email: normalizedEmail
    }
  });
}

export async function getAdminDashboardData(origin) {
  await ensureDefaultAdminRoles();

  const [roles, users, accessGrants, recentActivity, ambassadorMetrics, totals] = await Promise.all([
    prisma.adminRole.findMany({
      include: {
        _count: {
          select: {
            users: true
          }
        }
      },
      orderBy: [
        {
          isSystem: "desc"
        },
        {
          name: "asc"
        }
      ]
    }),
    prisma.user.findMany({
      include: {
        accessGrants: {
          orderBy: {
            createdAt: "desc"
          }
        },
        adminRole: true,
        ambassadorApplications: {
          orderBy: {
            createdAt: "desc"
          }
        },
        desktopTokens: {
          where: {
            revokedAt: null
          },
          orderBy: {
            createdAt: "desc"
          }
        },
        subscriptions: {
          orderBy: {
            createdAt: "desc"
          }
        }
      },
      orderBy: {
        createdAt: "desc"
      }
    }),
    prisma.accessGrant.findMany({
      include: {
        grantedByUser: {
          select: {
            displayName: true,
            email: true
          }
        },
        user: {
          select: {
            displayName: true,
            email: true
          }
        }
      },
      orderBy: {
        createdAt: "desc"
      },
      take: 24
    }),
    prisma.adminActivityLog.findMany({
      include: {
        actorUser: {
          select: {
            displayName: true,
            email: true
          }
        }
      },
      orderBy: {
        createdAt: "desc"
      },
      take: 12
    }),
    getAmbassadorMetrics(origin),
    Promise.all([
      prisma.user.count(),
      prisma.user.count({
        where: {
          role: "ADMIN"
        }
      }),
      prisma.user.count({
        where: {
          createdAt: {
            gte: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)
          }
        }
      }),
      prisma.subscription.count({
        where: {
          stripeStatus: {
            in: Array.from(ACTIVE_SUBSCRIPTION_STATUSES)
          }
        }
      }),
      prisma.desktopToken.count({
        where: {
          revokedAt: null,
          OR: [
            {
              expiresAt: null
            },
            {
              expiresAt: {
                gt: new Date()
              }
            }
          ]
        }
      }),
      prisma.accessGrant.count({
        where: {
          startsAt: {
            lte: new Date()
          },
          revokedAt: null,
          OR: [
            {
              endsAt: null
            },
            {
              endsAt: {
                gte: new Date()
              }
            }
          ]
        }
      })
    ])
  ]);

  const summarizedUsers = users.map((user) => summarizeUser(user, origin));
  const activePremiumUsers = summarizedUsers.filter((user) => user.access.hasPremiumAccess).length;
  const summary = {
    totalUsers: totals[0],
    adminUsers: totals[1],
    newUsersLast30Days: totals[2],
    activeSubscriptions: totals[3],
    activeDesktopSessions: totals[4],
    activeManualGrants: totals[5],
    activePremiumUsers,
    pendingAmbassadors: ambassadorMetrics.pendingCount,
    approvedAmbassadors: ambassadorMetrics.approvedCount,
    grantedAmbassadorAccesses: ambassadorMetrics.grantedAccessCount,
    referredRevenue: ambassadorMetrics.referredRevenue,
    commissionDue: ambassadorMetrics.commissionDue
  };

  return {
    permissionCatalog: ADMIN_PERMISSION_GROUPS,
    roles: roles.map((role) => ({
      ...role,
      memberCount: role._count.users,
      permissions: normalizePermissions(role.permissions)
    })),
    users: summarizedUsers,
    accessGrants: accessGrants.map((grant) => ({
      ...grant,
      isActive: isManualGrantActive(grant)
    })),
    recentActivity,
    ambassadorMetrics,
    summary,
    analytics: buildAnalyticsSnapshot({
      users,
      summary
    })
  };
}

export async function createManagedUser(input, actorUserId) {
  const email = normalizeEmail(input.email);
  const password = sanitizeText(input.password);
  const displayName = sanitizeText(input.displayName) || null;
  const adminRoleId = await resolveAdminRoleId(input.adminRoleId);

  if (!email || !password) {
    throw new Error("EMAIL_AND_PASSWORD_REQUIRED");
  }

  const passwordHash = await hashPassword(password);
  const user = await prisma.user.create({
    data: {
      email,
      passwordHash,
      displayName,
      role: adminRoleId ? "ADMIN" : "USER",
      adminRoleId
    },
    include: {
      adminRole: true
    }
  });

  await attachPendingUserRecordsByEmail({
    userId: user.id,
    email: user.email
  });

  await recordAdminActivity(
    actorUserId,
    "user.created",
    "user",
    `Compte ${user.email} cree${adminRoleId ? " avec acces admin" : ""}.`,
    displayName || "",
    user.id
  );

  return user;
}

export async function updateManagedUser(userId, updates, actorUserId) {
  const normalizedUserId = sanitizeText(userId);
  const currentUser = await prisma.user.findUnique({
    where: {
      id: normalizedUserId
    },
    include: {
      adminRole: true
    }
  });

  if (!currentUser) {
    throw new Error("USER_NOT_FOUND");
  }

  const nextAdminRoleId = Object.prototype.hasOwnProperty.call(updates, "adminRoleId")
    ? await resolveAdminRoleId(updates.adminRoleId)
    : currentUser.adminRoleId;

  if (actorUserId === currentUser.id && !nextAdminRoleId) {
    throw new Error("SELF_ROLE_REMOVAL_FORBIDDEN");
  }

  const email = sanitizeText(updates.email)
    ? normalizeEmail(updates.email)
    : currentUser.email;
  const password = sanitizeText(updates.password);

  const data = {
    email,
    displayName: sanitizeText(updates.displayName, currentUser.displayName || "") || null,
    adminRoleId: nextAdminRoleId,
    role: nextAdminRoleId ? "ADMIN" : "USER"
  };

  if (password) {
    data.passwordHash = await hashPassword(password);
  }

  const user = await prisma.user.update({
    where: {
      id: normalizedUserId
    },
    data,
    include: {
      adminRole: true
    }
  });

  await attachPendingUserRecordsByEmail({
    userId: user.id,
    email: user.email
  });

  await recordAdminActivity(
    actorUserId,
    "user.updated",
    "user",
    `Compte ${user.email} mis a jour.`,
    nextAdminRoleId ? `Role admin: ${user.adminRole?.name || "assigne"}` : "Aucun role admin",
    user.id
  );

  return user;
}

export async function createAdminRole(input, actorUserId) {
  const name = sanitizeText(input.name);
  const description = sanitizeText(input.description) || null;
  const rawKey = sanitizeText(input.key) || name;
  const key = sanitizeRoleKey(rawKey);
  const permissions = normalizePermissions(input.permissions);

  if (!name || !key) {
    throw new Error("ROLE_NAME_REQUIRED");
  }

  const role = await prisma.adminRole.create({
    data: {
      key,
      name,
      description,
      permissions,
      isSystem: false
    }
  });

  await recordAdminActivity(
    actorUserId,
    "role.created",
    "admin-role",
    `Role ${role.name} cree.`,
    permissions.join(", "),
    role.id
  );

  return role;
}

export async function updateAdminRole(roleId, input, actorUserId) {
  const normalizedRoleId = sanitizeText(roleId);
  const currentRole = await prisma.adminRole.findUnique({
    where: {
      id: normalizedRoleId
    }
  });

  if (!currentRole) {
    throw new Error("ROLE_NOT_FOUND");
  }

  const isOwnerRole = currentRole.key === "owner";
  const name = sanitizeText(input.name, currentRole.name) || currentRole.name;
  const description = sanitizeText(input.description, currentRole.description || "") || null;
  const key = currentRole.isSystem
    ? currentRole.key
    : sanitizeRoleKey(sanitizeText(input.key, currentRole.key) || currentRole.key);
  const permissions = isOwnerRole
    ? getAllAdminPermissionKeys()
    : normalizePermissions(input.permissions?.length ? input.permissions : currentRole.permissions);

  const role = await prisma.adminRole.update({
    where: {
      id: normalizedRoleId
    },
    data: {
      name,
      description,
      key,
      permissions
    }
  });

  await recordAdminActivity(
    actorUserId,
    "role.updated",
    "admin-role",
    `Role ${role.name} mis a jour.`,
    permissions.join(", "),
    role.id
  );

  return role;
}

export async function createManualAccessGrant(input, actorUserId) {
  const user = await resolveUserByEmailOrId(input);

  if (!user) {
    throw new Error("USER_NOT_FOUND");
  }

  const reason = sanitizeText(input.reason);

  if (!reason) {
    throw new Error("ACCESS_REASON_REQUIRED");
  }

  const startsAt = parseDate(input.startsAt) || new Date();
  const endsAt = parseDate(input.endsAt);

  if (endsAt && endsAt < startsAt) {
    throw new Error("ACCESS_END_BEFORE_START");
  }

  const grant = await prisma.accessGrant.create({
    data: {
      userId: user.id,
      type: "PREMIUM",
      reason,
      notes: sanitizeText(input.notes) || null,
      startsAt,
      endsAt,
      grantedByUserId: actorUserId || null
    },
    include: {
      user: {
        select: {
          displayName: true,
          email: true
        }
      }
    }
  });

  await recordAdminActivity(
    actorUserId,
    "access-grant.created",
    "access-grant",
    `Acces premium manuel accorde a ${grant.user.email}.`,
    reason,
    grant.id
  );

  return grant;
}

export async function revokeManualAccessGrant(grantId, actorUserId) {
  const normalizedGrantId = sanitizeText(grantId);
  const currentGrant = await prisma.accessGrant.findUnique({
    where: {
      id: normalizedGrantId
    },
    include: {
      user: {
        select: {
          email: true
        }
      }
    }
  });

  if (!currentGrant) {
    throw new Error("ACCESS_GRANT_NOT_FOUND");
  }

  const grant = await prisma.accessGrant.update({
    where: {
      id: normalizedGrantId
    },
    data: {
      revokedAt: new Date()
    }
  });

  await recordAdminActivity(
    actorUserId,
    "access-grant.revoked",
    "access-grant",
    `Acces premium manuel revoque pour ${currentGrant.user.email}.`,
    currentGrant.reason,
    grant.id
  );

  return grant;
}

export async function createAdminAmbassadorApplication(input, actorUserId) {
  const application = await createAmbassadorApplication({
    fullName: sanitizeText(input.fullName),
    email: normalizeEmail(input.email),
    channelName: sanitizeText(input.channelName),
    platform: sanitizeText(input.platform),
    channelUrl: sanitizeText(input.channelUrl),
    audienceSummary: sanitizeText(input.audienceSummary),
    motivation: sanitizeText(input.motivation),
    status: sanitizeText(input.status) || "approved",
    commissionRate: normalizeCommissionRateInput(input.commissionRate, 0.1),
    accessGranted: input.accessGranted,
    affiliateCode: sanitizeText(input.affiliateCode),
    referredRevenue: normalizeMoney(input.referredRevenue, 0),
    paidOutAmount: normalizeMoney(input.paidOutAmount, 0),
    adminNotes: sanitizeText(input.adminNotes),
    userId: sanitizeText(input.userId)
  });

  await recordAdminActivity(
    actorUserId,
    "ambassador.created",
    "ambassador-application",
    `Ambassadeur ${application.email} cree depuis l'admin.`,
    application.status,
    application.id
  );

  return application;
}

export async function updateAdminAmbassadorApplication(applicationId, updates, actorUserId) {
  const application = await updateAmbassadorApplication(applicationId, updates);

  await recordAdminActivity(
    actorUserId,
    "ambassador.updated",
    "ambassador-application",
    `Candidature ambassadeur ${application.email} mise a jour.`,
    `Statut: ${application.status}`,
    application.id
  );

  return application;
}

export function isAdminMutationError(error) {
  return error instanceof Prisma.PrismaClientKnownRequestError && error.code === "P2002";
}
