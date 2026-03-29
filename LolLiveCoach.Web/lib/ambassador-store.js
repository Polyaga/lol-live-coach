import crypto from "node:crypto";
import { prisma } from "./prisma";
import { normalizeEmail } from "./security";
import { resolveSiteUrl } from "./site-config";

function normalizeMoney(value) {
  const parsed = Number.parseFloat(String(value ?? "0").replace(",", "."));
  return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
}

function sanitizeText(value, fallback = "") {
  return String(value ?? fallback).trim();
}

function normalizeStatus(value, fallback = "pending") {
  const normalized = sanitizeText(value, fallback).toLowerCase();
  return ["pending", "approved", "rejected"].includes(normalized) ? normalized : fallback;
}

function normalizeCommissionRate(value, fallback = 0.1) {
  const parsed = Number.parseFloat(String(value ?? fallback).replace(",", "."));
  const normalized = Number.isFinite(parsed) ? parsed : fallback;
  return Math.max(0, Math.min(normalized, 1));
}

function buildApplicationFilters({ userId, email } = {}) {
  const filters = [];
  const normalizedUserId = sanitizeText(userId);
  const normalizedUserEmail = normalizeEmail(email);

  if (normalizedUserId) {
    filters.push({
      userId: normalizedUserId
    });
  }

  if (normalizedUserEmail) {
    filters.push({
      email: {
        equals: normalizedUserEmail,
        mode: "insensitive"
      }
    });
  }

  return {
    filters,
    normalizedUserId,
    normalizedUserEmail
  };
}

function slugify(value) {
  return sanitizeText(value)
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

async function generateAffiliateCode(application) {
  const base = slugify(application.channelName || application.fullName || "coach") || "coach";

  for (let attempt = 0; attempt < 5; attempt += 1) {
    const suffix = crypto.randomBytes(2).toString("hex");
    const candidate = `${base}-${suffix}`;
    const existing = await prisma.ambassadorApplication.findUnique({
      where: {
        affiliateCode: candidate
      },
      select: {
        id: true
      }
    });

    if (!existing) {
      return candidate;
    }
  }

  return `${base}-${crypto.randomUUID().slice(0, 8)}`;
}

function decimalToNumber(value) {
  return Number(value ?? 0);
}

function normalizeApplication(application) {
  const referredRevenue = normalizeMoney(decimalToNumber(application.referredRevenue));
  const paidOutAmount = normalizeMoney(decimalToNumber(application.paidOutAmount));
  const commissionRate = decimalToNumber(application.commissionRate) || 0.1;
  const grossCommission = referredRevenue * commissionRate;
  const commissionDue = Math.max(0, grossCommission - paidOutAmount);

  return {
    id: sanitizeText(application.id),
    createdAt: application.createdAt?.toISOString?.() || application.createdAt || new Date().toISOString(),
    updatedAt: application.updatedAt?.toISOString?.() || application.updatedAt || new Date().toISOString(),
    fullName: sanitizeText(application.fullName),
    email: sanitizeText(application.email),
    channelName: sanitizeText(application.channelName),
    platform: sanitizeText(application.platform),
    channelUrl: sanitizeText(application.channelUrl),
    audienceSummary: sanitizeText(application.audienceSummary),
    motivation: sanitizeText(application.motivation),
    status: sanitizeText(application.status, "pending") || "pending",
    commissionRate,
    accessGranted: Boolean(application.accessGranted),
    accessGrantedAt: application.accessGrantedAt?.toISOString?.() || application.accessGrantedAt || null,
    affiliateCode: sanitizeText(application.affiliateCode),
    referredRevenue,
    paidOutAmount,
    grossCommission,
    commissionDue,
    adminNotes: sanitizeText(application.adminNotes),
    lastReferralAt: application.lastReferralAt?.toISOString?.() || application.lastReferralAt || null
  };
}

export async function listAmbassadorApplications() {
  const applications = await prisma.ambassadorApplication.findMany({
    orderBy: {
      createdAt: "desc"
    }
  });

  return applications.map(normalizeApplication);
}

export async function createAmbassadorApplication(input) {
  const normalizedEmail = normalizeEmail(input.email);
  let linkedUserId = sanitizeText(input.userId);

  if (!linkedUserId && normalizedEmail) {
    const existingUser = await prisma.user.findUnique({
      where: {
        email: normalizedEmail
      },
      select: {
        id: true
      }
    });

    linkedUserId = existingUser?.id || "";
  }

  const nextStatus = normalizeStatus(input.status, "pending");
  const nextAccessGranted = Boolean(input.accessGranted);
  const nextAffiliateCode = sanitizeText(input.affiliateCode);

  const created = await prisma.ambassadorApplication.create({
    data: {
      userId: linkedUserId || null,
      fullName: input.fullName,
      email: normalizedEmail,
      channelName: input.channelName,
      platform: input.platform || null,
      channelUrl: input.channelUrl,
      audienceSummary: input.audienceSummary || null,
      motivation: input.motivation,
      status: nextStatus,
      commissionRate: normalizeCommissionRate(input.commissionRate, 0.1),
      accessGranted: nextAccessGranted,
      accessGrantedAt: nextAccessGranted ? new Date() : null,
      affiliateCode: nextAffiliateCode || null,
      referredRevenue: normalizeMoney(input.referredRevenue ?? 0),
      paidOutAmount: normalizeMoney(input.paidOutAmount ?? 0),
      adminNotes: sanitizeText(input.adminNotes) || null
    }
  });

  const generatedAffiliateCode = !created.affiliateCode && created.status === "approved"
    ? await generateAffiliateCode(created)
    : "";

  const application = generatedAffiliateCode
    ? await prisma.ambassadorApplication.update({
      where: {
        id: created.id
      },
      data: {
        affiliateCode: generatedAffiliateCode
      }
    })
    : created;

  return normalizeApplication(application);
}

export async function updateAmbassadorApplication(id, updates) {
  const current = await prisma.ambassadorApplication.findUnique({
    where: {
      id
    }
  });

  if (!current) {
    throw new Error("Candidature ambassadeur introuvable.");
  }

  const nextStatus = normalizeStatus(updates.status, current.status) || current.status;
  const nextAccessGranted = Object.prototype.hasOwnProperty.call(updates, "accessGranted")
    ? Boolean(updates.accessGranted)
    : current.accessGranted;

  const affiliateCode = sanitizeText(updates.affiliateCode, current.affiliateCode || "");
  const nextAffiliateCode = affiliateCode
    || (nextStatus === "approved" && !current.affiliateCode
      ? await generateAffiliateCode(current)
      : current.affiliateCode);

  const updated = await prisma.ambassadorApplication.update({
    where: {
      id
    },
    data: {
      status: nextStatus,
      affiliateCode: nextAffiliateCode || null,
      referredRevenue: normalizeMoney(updates.referredRevenue ?? current.referredRevenue),
      paidOutAmount: normalizeMoney(updates.paidOutAmount ?? current.paidOutAmount),
      adminNotes: sanitizeText(updates.adminNotes, current.adminNotes || "") || null,
      accessGranted: nextAccessGranted,
      accessGrantedAt: nextAccessGranted
        ? current.accessGrantedAt || new Date()
        : null
    }
  });

  return normalizeApplication(updated);
}

export function getAffiliateLink(application, origin) {
  if (!application.affiliateCode) {
    return "";
  }

  return `${resolveSiteUrl(origin)}/?ref=${encodeURIComponent(application.affiliateCode)}`;
}

export async function getAmbassadorApplicationsForUser({ userId, email, origin } = {}) {
  const {
    filters,
    normalizedUserId,
    normalizedUserEmail
  } = buildApplicationFilters({
    userId,
    email
  });

  if (!filters.length) {
    return [];
  }

  if (normalizedUserId && normalizedUserEmail) {
    await prisma.ambassadorApplication.updateMany({
      where: {
        userId: null,
        email: {
          equals: normalizedUserEmail,
          mode: "insensitive"
        }
      },
      data: {
        userId: normalizedUserId,
        email: normalizedUserEmail
      }
    }).catch(() => null);
  }

  const applications = await prisma.ambassadorApplication.findMany({
    where: {
      OR: filters
    },
    orderBy: {
      createdAt: "desc"
    }
  });

  return applications.map((application) => {
    const normalized = normalizeApplication(application);

    return {
      ...normalized,
      affiliateLink: getAffiliateLink(normalized, origin)
    };
  });
}

export async function getLatestAmbassadorApplicationForUser(options = {}) {
  const applications = await getAmbassadorApplicationsForUser(options);
  return applications[0] || null;
}

export async function getAmbassadorMetrics(origin) {
  const applications = await listAmbassadorApplications();
  const pendingCount = applications.filter((application) => application.status === "pending").length;
  const approvedCount = applications.filter((application) => application.status === "approved").length;
  const grantedAccessCount = applications.filter((application) => application.accessGranted).length;
  const referredRevenue = applications.reduce((sum, application) => sum + application.referredRevenue, 0);
  const commissionDue = applications.reduce((sum, application) => sum + application.commissionDue, 0);

  return {
    pendingCount,
    approvedCount,
    grantedAccessCount,
    referredRevenue,
    commissionDue,
    applications: applications.map((application) => ({
      ...application,
      affiliateLink: getAffiliateLink(application, origin)
    }))
  };
}
