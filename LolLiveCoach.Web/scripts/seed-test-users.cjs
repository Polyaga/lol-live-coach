const crypto = require("node:crypto");
const path = require("node:path");
const { promisify } = require("node:util");

const dotenv = require("dotenv");
const { PrismaClient } = require("@prisma/client");
const { PrismaPg } = require("@prisma/adapter-pg");
const { Pool } = require("pg");

dotenv.config({ path: path.join(process.cwd(), ".env.local") });
dotenv.config({ path: path.join(process.cwd(), ".env") });

const scryptAsync = promisify(crypto.scrypt);
const PASSWORD_KEY_LENGTH = 64;

function getDatabaseUrl() {
  const databaseUrl = process.env.DATABASE_URL;

  if (!databaseUrl) {
    throw new Error("DATABASE_URL est requis pour executer le seed des comptes de test.");
  }

  return databaseUrl;
}

async function hashPassword(password) {
  const normalized = String(password || "");
  const salt = crypto.randomBytes(16).toString("hex");
  const derivedKey = await scryptAsync(normalized, salt, PASSWORD_KEY_LENGTH);

  return `scrypt:${salt}:${Buffer.from(derivedKey).toString("hex")}`;
}

async function upsertUser(prisma, definition) {
  const passwordHash = await hashPassword(definition.password);

  return prisma.user.upsert({
    where: {
      email: definition.email
    },
    create: {
      email: definition.email,
      displayName: definition.displayName,
      role: definition.role,
      adminRoleId: definition.adminRoleId || null,
      passwordHash,
      stripeCustomerId: definition.stripeCustomerId || null
    },
    update: {
      displayName: definition.displayName,
      role: definition.role,
      adminRoleId: definition.adminRoleId || null,
      passwordHash,
      stripeCustomerId: definition.stripeCustomerId || null
    }
  });
}

async function ensureAdminRoles(prisma) {
  const roles = [
    {
      key: "owner",
      name: "Owner",
      description: "Controle total du back-office.",
      permissions: [
        "dashboard.view",
        "analytics.view",
        "users.view",
        "users.manage",
        "roles.view",
        "roles.manage",
        "ambassadors.view",
        "ambassadors.manage",
        "access.view",
        "access.manage"
      ]
    }
  ];

  for (const role of roles) {
    await prisma.adminRole.upsert({
      where: {
        key: role.key
      },
      create: {
        ...role,
        isSystem: true
      },
      update: {
        name: role.name,
        description: role.description,
        permissions: role.permissions,
        isSystem: true
      }
    });
  }

  return prisma.adminRole.findUnique({
    where: {
      key: "owner"
    }
  });
}

async function seedAdmin(prisma) {
  const email = process.env.ADMIN_EMAIL || "admin@lol-live-coach.local";
  const password = process.env.ADMIN_PASSWORD || "test-admin";
  const ownerRole = await ensureAdminRoles(prisma);

  return upsertUser(prisma, {
    email,
    password,
    displayName: "Admin Test",
    role: "ADMIN",
    adminRoleId: ownerRole?.id || null
  });
}

async function seedFree(prisma) {
  return upsertUser(prisma, {
    email: "free@lol-live-coach.test",
    password: "Free12345!",
    displayName: "Free Test",
    role: "USER"
  });
}

async function seedPremium(prisma) {
  const user = await upsertUser(prisma, {
    email: "premium@lol-live-coach.test",
    password: "Premium123!",
    displayName: "Premium Test",
    role: "USER",
    stripeCustomerId: "cus_test_premium_local"
  });

  await prisma.subscription.upsert({
    where: {
      stripeSubscriptionId: "sub_test_premium_local"
    },
    create: {
      userId: user.id,
      stripeSubscriptionId: "sub_test_premium_local",
      stripeCustomerId: "cus_test_premium_local",
      stripePriceId: process.env.STRIPE_PRICE_MONTHLY || "price_test_monthly_local",
      stripeStatus: "active",
      planKey: "monthly",
      billingInterval: "month",
      currentPeriodEnd: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
      cancelAtPeriodEnd: false
    },
    update: {
      userId: user.id,
      stripeCustomerId: "cus_test_premium_local",
      stripePriceId: process.env.STRIPE_PRICE_MONTHLY || "price_test_monthly_local",
      stripeStatus: "active",
      planKey: "monthly",
      billingInterval: "month",
      currentPeriodEnd: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
      cancelAtPeriodEnd: false
    }
  });

  return user;
}

async function seedAmbassador(prisma) {
  const user = await upsertUser(prisma, {
    email: "ambassador@lol-live-coach.test",
    password: "Ambassador123!",
    displayName: "Ambassador Test",
    role: "USER"
  });

  await prisma.ambassadorApplication.upsert({
    where: {
      affiliateCode: "ambassador-test"
    },
    create: {
      userId: user.id,
      fullName: "Ambassador Test",
      email: user.email,
      channelName: "Ambassador Test Channel",
      platform: "Twitch",
      channelUrl: "https://twitch.tv/ambassador_test",
      audienceSummary: "Profil de test pour verifier l'acces ambassadeur premium.",
      motivation: "Compte cree pour tester le parcours ambassadeur et l'acces offert.",
      status: "approved",
      commissionRate: 0.1,
      accessGranted: true,
      accessGrantedAt: new Date(),
      affiliateCode: "ambassador-test",
      referredRevenue: 250,
      paidOutAmount: 25,
      adminNotes: "Seed de demonstration pour l'admin."
    },
    update: {
      userId: user.id,
      fullName: "Ambassador Test",
      email: user.email,
      channelName: "Ambassador Test Channel",
      platform: "Twitch",
      channelUrl: "https://twitch.tv/ambassador_test",
      audienceSummary: "Profil de test pour verifier l'acces ambassadeur premium.",
      motivation: "Compte cree pour tester le parcours ambassadeur et l'acces offert.",
      status: "approved",
      commissionRate: 0.1,
      accessGranted: true,
      accessGrantedAt: new Date(),
      referredRevenue: 250,
      paidOutAmount: 25,
      adminNotes: "Seed de demonstration pour l'admin."
    }
  });

  return user;
}

async function main() {
  const pool = new Pool({
    connectionString: getDatabaseUrl()
  });

  const prisma = new PrismaClient({
    adapter: new PrismaPg(pool)
  });

  try {
    const admin = await seedAdmin(prisma);
    const free = await seedFree(prisma);
    const premium = await seedPremium(prisma);
    const ambassador = await seedAmbassador(prisma);

    console.log("Comptes de test prets :");
    console.log(`- admin      : ${admin.email}`);
    console.log(`- free       : ${free.email} / Free12345!`);
    console.log(`- premium    : ${premium.email} / Premium123!`);
    console.log(`- ambassadeur: ${ambassador.email} / Ambassador123!`);
    console.log("- admin web  : peut se connecter via /login ou /admin/login avec le compte admin seed.");
  } finally {
    await prisma.$disconnect();
    await pool.end();
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
