import { NextResponse } from "next/server";
import { getCurrentUser } from "../../../../lib/auth";
import { createAmbassadorApplication } from "../../../../lib/ambassador-store";

export const runtime = "nodejs";

function getTrimmedValue(formData, key) {
  return String(formData.get(key) || "").trim();
}

export async function POST(request) {
  const formData = await request.formData();
  const user = await getCurrentUser();

  const payload = {
    userId: user?.id || null,
    fullName: getTrimmedValue(formData, "fullName"),
    email: getTrimmedValue(formData, "email"),
    channelName: getTrimmedValue(formData, "channelName"),
    platform: getTrimmedValue(formData, "platform"),
    channelUrl: getTrimmedValue(formData, "channelUrl"),
    audienceSummary: getTrimmedValue(formData, "audienceSummary"),
    motivation: getTrimmedValue(formData, "motivation")
  };

  if (!payload.fullName || !payload.email || !payload.channelName || !payload.channelUrl || !payload.motivation) {
    return NextResponse.redirect(new URL("/ambassadeur?error=1", request.url), 303);
  }

  await createAmbassadorApplication(payload);
  return NextResponse.redirect(new URL("/ambassadeur?submitted=1", request.url), 303);
}
