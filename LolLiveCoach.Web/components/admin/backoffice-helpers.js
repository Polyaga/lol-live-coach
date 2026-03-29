export const NOTICE_MESSAGES = {
  "user-created": "Compte cree avec succes.",
  "user-updated": "Compte mis a jour.",
  "role-created": "Role cree avec succes.",
  "role-updated": "Role mis a jour.",
  "access-created": "Acces premium manuel accorde.",
  "access-revoked": "Acces premium manuel revoque.",
  "ambassador-created": "Ambassadeur cree depuis l'admin.",
  "ambassador-updated": "Candidature ambassadeur mise a jour."
};

export const ERROR_MESSAGES = {
  forbidden: "Vous n'avez pas les permissions necessaires pour cette action.",
  "duplicate-email": "Cet email est deja utilise par un autre compte.",
  "duplicate-role-key": "Cette cle de role existe deja.",
  "missing-user": "Utilisateur introuvable.",
  "missing-role": "Role introuvable.",
  "missing-access-grant": "Acces manuel introuvable.",
  "missing-application": "Candidature ambassadeur introuvable.",
  "self-role-removal": "Vous ne pouvez pas retirer votre propre role admin.",
  "password-too-short": "Le mot de passe doit contenir au moins 8 caracteres.",
  "missing-fields": "Des champs requis sont manquants."
};

export const ADMIN_SECTION_LABELS = {
  access: "Acces premium",
  ambassadors: "Ambassadeurs",
  analytics: "Stats",
  dashboard: "Dashboard",
  roles: "Roles",
  users: "Utilisateurs"
};

export function formatMoney(amount) {
  return new Intl.NumberFormat("fr-FR", {
    style: "currency",
    currency: "EUR",
    maximumFractionDigits: 2
  }).format(amount || 0);
}

export function formatDate(value, fallback = "Jamais") {
  if (!value) {
    return fallback;
  }

  return new Intl.DateTimeFormat("fr-FR", {
    dateStyle: "medium",
    timeStyle: "short"
  }).format(new Date(value));
}

export function formatPercent(value) {
  return `${Math.round(Number(value || 0) * 100)}%`;
}

export function statusLabel(status) {
  switch (String(status || "").toLowerCase()) {
    case "approved":
      return "Approuve";
    case "rejected":
      return "Refuse";
    default:
      return "En attente";
  }
}

export function grantStatus(grant) {
  if (grant.revokedAt) {
    return {
      label: "Revoque",
      tone: "status-rejected"
    };
  }

  if (grant.isActive) {
    return {
      label: "Actif",
      tone: "status-approved"
    };
  }

  return {
    label: "Expire",
    tone: "status-pending"
  };
}

export function accessSourceLabel(access) {
  switch (access?.premiumSource) {
    case "subscription":
      return "Abonnement";
    case "ambassador":
      return "Programme ambassadeur";
    case "manual":
      return "Grant manuel";
    default:
      return "Free";
  }
}

export function actorLabel(activity) {
  return activity.actorUser?.displayName || activity.actorUser?.email || "Equipe admin";
}

export function getVisibleAdminSections({ capabilities, data }) {
  return [
    capabilities.canViewDashboard && {
      id: "dashboard",
      label: "Dashboard",
      title: "Pilotage",
      description: "KPI globaux, activite recente et synthese rapide.",
      meta: `${data.summary.totalUsers} comptes`
    },
    capabilities.canViewUsers && {
      id: "users",
      label: "Utilisateurs",
      title: "Comptes",
      description: "Comptes clients, equipe interne et niveau d'acces.",
      meta: `${data.users.length} utilisateurs`
    },
    capabilities.canViewAnalytics && {
      id: "analytics",
      label: "Stats",
      title: "KPI",
      description: "Acquisition, abonnements, churn et croissance nette.",
      meta: `${data.analytics.totals.netGrowth > 0 ? "+" : ""}${data.analytics.totals.netGrowth} net`
    },
    capabilities.canViewRoles && {
      id: "roles",
      label: "Roles",
      title: "Permissions",
      description: "Roles admin systemes et roles personnalises.",
      meta: `${data.roles.length} roles`
    },
    capabilities.canViewAccess && {
      id: "access",
      label: "Acces premium",
      title: "Grants",
      description: "Acces premium accordes manuellement et leur historique.",
      meta: `${data.summary.activeManualGrants} actifs`
    },
    capabilities.canViewAmbassadors && {
      id: "ambassadors",
      label: "Ambassadeurs",
      title: "Partenaires",
      description: "Candidatures, liens d'affiliation et commissions.",
      meta: `${data.summary.pendingAmbassadors} en attente`
    }
  ].filter(Boolean);
}

export function normalizeAdminSection(requestedSection, sections) {
  const fallback = sections[0]?.id || "dashboard";
  const normalizedSection = String(requestedSection || "").trim().toLowerCase();

  return sections.some((section) => section.id === normalizedSection)
    ? normalizedSection
    : fallback;
}

export function matchesUserSearch(user, query) {
  const normalizedQuery = String(query || "").trim().toLowerCase();

  if (!normalizedQuery) {
    return true;
  }

  const haystack = [
    user.displayName,
    user.email
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();
  const terms = normalizedQuery.split(/\s+/).filter(Boolean);

  return terms.every((term) => haystack.includes(term));
}
