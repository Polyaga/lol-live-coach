import { prisma } from "./prisma";
import { normalizeEmail } from "./security";

export async function attachPendingUserRecordsByEmail({ userId, email }) {
  const normalizedEmail = normalizeEmail(email);

  if (!userId || !normalizedEmail) {
    return;
  }

  const pendingApplications = await prisma.ambassadorApplication.findMany({
    where: {
      userId: null
    },
    select: {
      id: true,
      email: true
    }
  });

  const matchingApplicationIds = pendingApplications
    .filter((application) => normalizeEmail(application.email) === normalizedEmail)
    .map((application) => application.id);

  if (matchingApplicationIds.length === 0) {
    return;
  }

  await prisma.ambassadorApplication.updateMany({
    where: {
      id: {
        in: matchingApplicationIds
      }
    },
    data: {
      userId
    }
  });
}
