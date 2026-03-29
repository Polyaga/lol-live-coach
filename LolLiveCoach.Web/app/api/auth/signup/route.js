import { Prisma } from "@prisma/client";
import { NextResponse } from "next/server";
import { appendQueryToPath, getSafeRedirectPath } from "../../../../lib/navigation";
import { signUpUser } from "../../../../lib/auth";

export const runtime = "nodejs";

export async function POST(request) {
  const formData = await request.formData();
  const email = String(formData.get("email") || "").trim();
  const password = String(formData.get("password") || "").trim();
  const displayName = String(formData.get("displayName") || "").trim();
  const next = getSafeRedirectPath(formData.get("next"), "/compte");
  const plan = String(formData.get("plan") || "").trim();

  try {
    await signUpUser({
      email,
      password,
      displayName
    });
  } catch (error) {
    const duplicate =
      (error instanceof Prisma.PrismaClientKnownRequestError && error.code === "P2002")
      || (error instanceof Error && error.message === "DUPLICATE_EMAIL");
    const target = appendQueryToPath("/signup", {
      error: duplicate ? "" : 1,
      duplicate: duplicate ? 1 : "",
      next,
      plan
    });

    return NextResponse.redirect(new URL(target, request.url), 303);
  }

  return NextResponse.redirect(
    new URL(appendQueryToPath("/login", {
      created: 1,
      next,
      plan
    }), request.url),
    303
  );
}
