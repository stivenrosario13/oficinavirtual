import { NextResponse } from "next/server";
import { getPendingGroups } from "@/server/agencies";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET() {
  try {
    const groups = await getPendingGroups();
    return NextResponse.json({ groups });
  } catch (err) {
    console.error("GET /api/public/groups", err);
    return NextResponse.json(
      { error: "No se pudieron cargar los grupos." },
      { status: 500 },
    );
  }
}
