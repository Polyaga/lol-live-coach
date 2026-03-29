import { prisma } from "./prisma";
import { getAmbassadorApplicationsForUser } from "./ambassador-store";

export async function getAccountOverviewByUserId(userId, options = {}) {
  const account = await prisma.user.findUnique({
    where: {
      id: userId
    },
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
      desktopTokens: {
        where: {
          revokedAt: null
        },
        orderBy: {
          createdAt: "desc"
        }
      }
    }
  });

  if (!account) {
    return null;
  }

  const ambassadorApplications = await getAmbassadorApplicationsForUser({
    userId: account.id,
    email: account.email,
    origin: options.origin
  });

  return {
    ...account,
    ambassadorApplications
  };
}
