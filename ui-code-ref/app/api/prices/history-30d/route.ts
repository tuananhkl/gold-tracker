import { getHistorySeries } from "@/lib/api"

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url)
  const brand = searchParams.get("brand")
  const regionParam = searchParams.get("region")
  const daysParam = searchParams.get("days")

  if (!brand) {
    return Response.json({ error: "brand query parameter is required" }, { status: 400 })
  }

  const days = daysParam ? Number.parseInt(daysParam, 10) : 30

  try {
    const history = await getHistorySeries({ brand, region: regionParam, days })
    return Response.json(history)
  } catch (error) {
    console.error("Failed to load price history", error)
    return Response.json({ error: "Failed to load price history" }, { status: 500 })
  }
}
