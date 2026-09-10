import { NextResponse } from "next/server";
import { getPendingAgencies } from "@/server/agencies";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  try {
    const grupo = new URL(request.url).searchParams.get("grupo");
    if (!grupo) {
      return NextResponse.json(
        { error: "Debes indicar un grupo." },
        { status: 400 },
      );
    }
    const agencies = await getPendingAgencies(grupo);
    return NextResponse.json({ agencies });
  } catch (err) {
    console.error("GET /api/public/agencies", err);
    return NextResponse.json(
      { error: "No se pudieron cargar las agencias." },
      { status: 500 },
    );
  }
}
