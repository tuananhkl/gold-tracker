import { getLatestTableRows } from "@/lib/api"

export async function GET(request: Request) {
  try {
    const { searchParams } = new URL(request.url)
    const dateParam = searchParams.get("date")
    
    if (!dateParam) {
      return Response.json({ error: "date parameter is required (format: YYYY-MM-DD)" }, { status: 400 })
    }

    // Parse date from YYYY-MM-DD to DD/MM/YYYY
    const dateParts = dateParam.split("-")
    if (dateParts.length !== 3) {
      return Response.json({ error: "Invalid date format. Expected YYYY-MM-DD" }, { status: 400 })
    }
    const selectedDate = `${dateParts[2]}/${dateParts[1]}/${dateParts[0]}`

    const rows = await getLatestTableRows(selectedDate)
    return Response.json(rows)
  } catch (error) {
    console.error("Failed to load price table by date", error)
    return Response.json({ error: "Failed to load price data" }, { status: 500 })
  }
}

