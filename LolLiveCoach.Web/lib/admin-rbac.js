import { prisma } from "./prisma";
import { hashPassword, normalizeEmail } from "./security";
import { attachPendingUserRecordsByEmail } from "./user-linking";

export const ADMIN_PERMISSIONS = {
  DASHBOARD_VIEW: "dashboard.view",
  ANALYTICS_VIEW: "analytics.view",
  USERS_VIEW: "users.view",
  USERS_MANAGE: "users.manage",
  ROLES_VIEW: "roles.view",
  ROLES_MANAGE: "roles.manage",
  AMBASSADORS_VIEW: "ambassadors.view",
  AMBASSADORS_MANAGE: "ambassadors.manage",
  ACCESS_VIEW: "access.view",
  ACCESS_MANAGE: "access.manage"
};

export const ADMIN_PERMISSION_GROUPS = [
  {
    id: "pilotage",
    label: "Pilotage",
    permissions: [
      {
        key: ADMIN_PERMISSIONS.DASHBOARD_VIEW,
        label: "Voir le tableau de bord",
        description: "Acces aux KPI globaux, aux syntheses et a l'activite recente."
      },
      {
        key: ADMIN_PERMISSIONS.ANALYTICS_VIEW,
        label: "Voir les statistiques",
        description: "Acces aux chiffres d'adoption, d'abonnement et d'ambassadeurs."
      }
    ]
  },
  {
    id: "utilisateurs",
    label: "Utilisateurs",
    permissions: [
      {
        key: ADMIN_PERMISSIONS.USERS_VIEW,
        label: "Voir les utilisateurs",
        description: "Acces aux comptes, abonnements, sessions desktop et roles assignes."
      },
      {
        key: ADMIN_PERMISSIONS.USERS_MANAGE,
        label: "Gerer les utilisateurs",
        description: "Creation de comptes, edition du profil, mot de passe et role admin."
      }
    ]
  },
  {
    id: "roles",
    label: "Roles",
    permissions: [
      {
        key: ADMIN_PERMISSIONS.ROLES_VIEW,
        label: "Voir les roles",
        description: "Acces au catalogue des roles et a la matrice des permissions."
      },
      {
        key: ADMIN_PERMISSIONS.ROLES_MANAGE,
        label: "Gerer les roles",
        description: "Creation ou modification des roles et de leurs permissions."
      }
    ]
  },
  {
    id: "ambassadeurs",
    label: "Ambassadeurs",
    permissions: [
      {
        key: ADMIN_PERMISSIONS.AMBASSADORS_VIEW,
        label: "Voir les ambassadeurs",
        description: "Acces aux candidatures, liens, commissions et etats d'avancement."
      },
      {
        key: ADMIN_PERMISSIONS.AMBASSADORS_MANAGE,
        label: "Gerer les ambassadeurs",
        description: "Creation manuelle, validation, notes, revenus et acces offerts."
      }
    ]
  },
  {
    id: "acces",
    label: "Acces applicatif",
    permissions: [
      {
        key: ADMIN_PERMISSIONS.ACCESS_VIEW,
        label: "Voir les acces",
        description: "Acces aux grants manuels premium et a leur historique."
      },
      {
        key: ADMIN_PERMISSIONS.ACCESS_MANAGE,
        label: "Gerer les acces",
        description: "Accorder ou revoquer un acces premium manuel sans intervention dev."
      }
    ]
  }
];

export const SYSTEM_ADMIN_ROLE_DEFINITIONS = [
  {
    key: "owner",
    name: "Owner",
    description: "Controle total du back-office, des roles et de la plateforme.",
    permissions: ADMIN_PERMISSION_GROUPS.flatMap((group) => group.permissions.map((permission) => permission.key)),
    isSystem: true
  },
  {
    key: "operations",
    name: "Operations",
    description: "Pilotage quotidien de la plateforme, des comptes et des acces.",
    permissions: [
      ADMIN_PERMISSIONS.DASHBOARD_VIEW,
      ADMIN_PERMISSIONS.ANALYTICS_VIEW,
      ADMIN_PERMISSIONS.USERS_VIEW,
      ADMIN_PERMISSIONS.USERS_MANAGE,
      ADMIN_PERMISSIONS.ROLES_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_MANAGE,
      ADMIN_PERMISSIONS.ACCESS_VIEW,
      ADMIN_PERMISSIONS.ACCESS_MANAGE
    ],
    isSystem: true
  },
  {
    key: "support",
    name: "Support",
    description: "Accompagne les utilisateurs et traite les acces sans toucher aux roles.",
    permissions: [
      ADMIN_PERMISSIONS.DASHBOARD_VIEW,
      ADMIN_PERMISSIONS.USERS_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_VIEW,
      ADMIN_PERMISSIONS.ACCESS_VIEW,
      ADMIN_PERMISSIONS.ACCESS_MANAGE
    ],
    isSystem: true
  },
  {
    key: "ambassador-manager",
    name: "Ambassador Manager",
    description: "Suit le programme ambassadeur, les validations et la performance.",
    permissions: [
      ADMIN_PERMISSIONS.DASHBOARD_VIEW,
      ADMIN_PERMISSIONS.ANALYTICS_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_MANAGE,
      ADMIN_PERMISSIONS.ACCESS_VIEW
    ],
    isSystem: true
  },
  {
    key: "analyst",
    name: "Analyst",
    description: "Lit les donnees globales de la plateforme sans pouvoir les modifier.",
    permissions: [
      ADMIN_PERMISSIONS.DASHBOARD_VIEW,
      ADMIN_PERMISSIONS.ANALYTICS_VIEW,
      ADMIN_PERMISSIONS.USERS_VIEW,
      ADMIN_PERMISSIONS.ROLES_VIEW,
      ADMIN_PERMISSIONS.AMBASSADORS_VIEW,
      ADMIN_PERMISSIONS.ACCESS_VIEW
    ],
    isSystem: true
  }
];

export function getAllAdminPermissionKeys() {
  return ADMIN_PERMISSION_GROUPS.flatMap((group) => group.permissions.map((permission) => permission.key));
}

export function sanitizeRoleKey(value) {
  return String(value || "")
    .trim()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

export function normalizePermissions(values) {
  const allowed = new Set(getAllAdminPermissionKeys());
  const uniquePermissions = new Set(
    []
      .concat(values || [])
      .map((value) => String(value || "").trim())
      .filter(Boolean)
      .filter((value) => allowed.has(value))
  );

  return Array.from(uniquePermissions);
}

export function hasAdminAccess(user) {
  return Boolean(user && (user.role === "ADMIN" || user.adminRoleId));
}

export function getUserAdminPermissions(user) {
  if (!user) {
    return [];
  }

  if (user.adminRole?.permissions?.length) {
    return normalizePermissions(user.adminRole.permissions);
  }

  if (user.role === "ADMIN") {
    return getAllAdminPermissionKeys();
  }

  return [];
}

export function userHasPermission(user, permission) {
  return getUserAdminPermissions(user).includes(permission);
}

export function getLegacyAdminConfig() {
  return {
    email: normalizeEmail(process.env.ADMIN_EMAIL || "admin@lol-live-coach.local"),
    password: String(process.env.ADMIN_PASSWORD || "").trim()
  };
}

export async function ensureDefaultAdminRoles() {
  const definitions = SYSTEM_ADMIN_ROLE_DEFINITIONS.map((definition) => ({
    ...definition,
    permissions: normalizePermissions(definition.permissions)
  }));
  const existingRoles = await prisma.adminRole.findMany({
    select: {
      key: true
    }
  });
  const existingKeys = new Set(existingRoles.map((role) => role.key));
  const missingRoles = definitions.filter((role) => !existingKeys.has(role.key));

  if (missingRoles.length > 0) {
    await prisma.adminRole.createMany({
      data: missingRoles
    });
  }
}

export async function getOwnerAdminRole() {
  await ensureDefaultAdminRoles();

  return prisma.adminRole.findUnique({
    where: {
      key: "owner"
    }
  });
}

export async function ensureAdminUserRoleAssignment(userId) {
  const user = await prisma.user.findUnique({
    where: {
      id: userId
    },
    include: {
      adminRole: true
    }
  });

  if (!user || user.adminRoleId || user.role !== "ADMIN") {
    return user;
  }

  const ownerRole = await getOwnerAdminRole();

  if (!ownerRole) {
    return user;
  }

  return prisma.user.update({
    where: {
      id: userId
    },
    data: {
      adminRoleId: ownerRole.id
    },
    include: {
      adminRole: true
    }
  });
}

export async function authenticateWithLegacyAdminBootstrap(email, password) {
  const config = getLegacyAdminConfig();
  const normalizedEmail = normalizeEmail(email);

  if (!config.password || normalizedEmail !== config.email || String(password || "") !== config.password) {
    return null;
  }

  const ownerRole = await getOwnerAdminRole();

  if (!ownerRole) {
    return null;
  }

  const passwordHash = await hashPassword(config.password);
  const existingUser = await prisma.user.findUnique({
    where: {
      email: normalizedEmail
    },
    select: {
      id: true
    }
  });

  const user = existingUser
    ? await prisma.user.update({
      where: {
        id: existingUser.id
      },
      data: {
        passwordHash,
        role: "ADMIN",
        adminRoleId: ownerRole.id
      },
      include: {
        adminRole: true
      }
    })
    : await prisma.user.create({
      data: {
        email: normalizedEmail,
        passwordHash,
        displayName: "Owner",
        role: "ADMIN",
        adminRoleId: ownerRole.id
      },
      include: {
        adminRole: true
      }
    });

  await attachPendingUserRecordsByEmail({
    userId: user.id,
    email: user.email
  });

  return user;
}
