import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { prisma } from "./prisma";
import { authenticateWithLegacyAdminBootstrap } from "./admin-rbac";
import {
  createOpaqueToken,
  hashPassword,
  normalizeEmail,
  sha256,
  verifyPassword
} from "./security";
import { attachPendingUserRecordsByEmail } from "./user-linking";

const WEB_SESSION_COOKIE = "llc_session";
const WEB_SESSION_MAX_AGE_SECONDS = 60 * 60 * 24 * 30;
const DESKTOP_TOKEN_MAX_AGE_DAYS = 180;

function buildCookieOptions(value, maxAge = WEB_SESSION_MAX_AGE_SECONDS) {
  return {
    name: WEB_SESSION_COOKIE,
    value,
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge
  };
}

async function getRequestMeta() {
  const headerStore = await headers();

  return {
    userAgent: headerStore.get("user-agent"),
    ipAddress:
      headerStore.get("x-forwarded-for")?.split(",")[0]?.trim()
      || headerStore.get("x-real-ip")
      || null
  };
}

export function buildSessionCookie(token) {
  return buildCookieOptions(token);
}

export function buildClearedSessionCookie() {
  return buildCookieOptions("", 0);
}

export async function signUpUser({ email, password, displayName }) {
  const normalizedEmail = normalizeEmail(email);

  if (!normalizedEmail) {
    throw new Error("L'email est requis.");
  }

  const existingUser = await prisma.user.findUnique({
    where: {
      email: normalizedEmail
    },
    select: {
      id: true
    }
  });

  if (existingUser) {
    throw new Error("DUPLICATE_EMAIL");
  }

  const passwordHash = await hashPassword(password);

  const user = await prisma.user.create({
    data: {
      email: normalizedEmail,
      passwordHash,
      displayName: String(displayName || "").trim() || null
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

export async function authenticateUser(email, password) {
  const normalizedEmail = normalizeEmail(email);

  const user = await prisma.user.findUnique({
    where: {
      email: normalizedEmail
    },
    include: {
      adminRole: true
    }
  });

  if (user) {
    const isValid = await verifyPassword(password, user.passwordHash);

    if (isValid) {
      return user;
    }
  }

  return authenticateWithLegacyAdminBootstrap(normalizedEmail, password);
}

export async function createWebSession(userId) {
  const token = createOpaqueToken(32);
  const tokenHash = sha256(token);
  const expiresAt = new Date(Date.now() + WEB_SESSION_MAX_AGE_SECONDS * 1000);
  const meta = await getRequestMeta();

  await prisma.webSession.create({
    data: {
      userId,
      tokenHash,
      expiresAt,
      userAgent: meta.userAgent,
      ipAddress: meta.ipAddress
    }
  });

  return token;
}

export async function getCurrentSession() {
  const cookieStore = await cookies();
  const token = cookieStore.get(WEB_SESSION_COOKIE)?.value;

  if (!token) {
    return null;
  }

  const session = await prisma.webSession.findUnique({
    where: {
      tokenHash: sha256(token)
    },
    include: {
      user: {
        include: {
          adminRole: true
        }
      }
    }
  });

  if (!session) {
    return null;
  }

  if (session.expiresAt <= new Date()) {
    await prisma.webSession.delete({
      where: {
        id: session.id
      }
    }).catch(() => null);

    return null;
  }

  if (Date.now() - session.lastSeenAt.getTime() > 1000 * 60 * 60 * 6) {
    await prisma.webSession.update({
      where: {
        id: session.id
      },
      data: {
        lastSeenAt: new Date()
      }
    }).catch(() => null);
  }

  return session;
}

export async function getCurrentUser() {
  const session = await getCurrentSession();
  return session?.user ?? null;
}

export async function requireUser() {
  const user = await getCurrentUser();

  if (!user) {
    redirect("/login");
  }

  return user;
}

export async function revokeCurrentWebSession() {
  const cookieStore = await cookies();
  const token = cookieStore.get(WEB_SESSION_COOKIE)?.value;

  if (!token) {
    return;
  }

  await prisma.webSession.deleteMany({
    where: {
      tokenHash: sha256(token)
    }
  });
}

export async function createDesktopToken(userId, name = "Desktop Windows", expiresInDays = DESKTOP_TOKEN_MAX_AGE_DAYS) {
  const rawToken = createOpaqueToken(48);
  const tokenHash = sha256(rawToken);
  const expiresAt = expiresInDays
    ? new Date(Date.now() + expiresInDays * 24 * 60 * 60 * 1000)
    : null;

  const token = await prisma.desktopToken.create({
    data: {
      userId,
      name: String(name || "Desktop Windows").trim() || "Desktop Windows",
      tokenHash,
      expiresAt
    }
  });

  return {
    id: token.id,
    rawToken: `llc_${rawToken}`,
    expiresAt: token.expiresAt
  };
}

export async function verifyDesktopToken(rawToken) {
  const normalized = String(rawToken || "").trim().replace(/^Bearer\s+/i, "");
  const token = normalized.startsWith("llc_") ? normalized.slice(4) : normalized;

  if (!token) {
    return null;
  }

  const desktopToken = await prisma.desktopToken.findUnique({
    where: {
      tokenHash: sha256(token)
    },
    include: {
      user: {
        include: {
          accessGrants: {
            orderBy: {
              createdAt: "desc"
            }
          },
          adminRole: true,
          subscriptions: {
            orderBy: {
              createdAt: "desc"
            }
          },
          ambassadorApplications: {
            orderBy: {
              createdAt: "desc"
            }
          }
        }
      }
    }
  });

  if (!desktopToken || desktopToken.revokedAt || (desktopToken.expiresAt && desktopToken.expiresAt <= new Date())) {
    return null;
  }

  await prisma.desktopToken.update({
    where: {
      id: desktopToken.id
    },
    data: {
      lastUsedAt: new Date()
    }
  }).catch(() => null);

  return desktopToken;
}

export async function revokeDesktopTokenById(userId, tokenId) {
  await prisma.desktopToken.updateMany({
    where: {
      id: tokenId,
      userId
    },
    data: {
      revokedAt: new Date()
    }
  });
}

export async function revokeDesktopTokenByRawToken(rawToken) {
  const normalized = String(rawToken || "").trim().replace(/^Bearer\s+/i, "");
  const token = normalized.startsWith("llc_") ? normalized.slice(4) : normalized;

  if (!token) {
    return;
  }

  await prisma.desktopToken.updateMany({
    where: {
      tokenHash: sha256(token)
    },
    data: {
      revokedAt: new Date()
    }
  });
}
