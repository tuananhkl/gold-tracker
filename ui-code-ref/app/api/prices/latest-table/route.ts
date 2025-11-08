import { getLatestTableRows } from "@/lib/api"

export async function GET() {
  try {
    const rows = await getLatestTableRows()
    return Response.json(rows)
  } catch (error) {
    console.error("Failed to load latest price table", error)
    return Response.json({ error: "Failed to load price data" }, { status: 500 })
  }
}
