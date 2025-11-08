"use client"

import { useEffect, useMemo, useState } from "react"
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from "recharts"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"

interface PriceData {
  label: string
  brand: string | null
  region?: string | null
  buyToday?: number | null
  sellToday?: number | null
  buyYesterday?: number | null
  sellYesterday?: number | null
  buyChange?: number | null
  sellChange?: number | null
}

interface ChartData {
  date: string
  buy: number
  sell: number
}

interface HistoryResponse {
  brand: string
  region?: string | null
  history: ChartData[]
}

const formatNumber = (value?: number | null) => {
  if (value === null || value === undefined) return "—"
  return value.toLocaleString("vi-VN")
}

const trendLabel = (delta?: number | null) => {
  if (delta === null || delta === undefined) return ""
  const prefix = delta >= 0 ? "▲" : "▼"
  return `${prefix} ${Math.abs(delta).toLocaleString("vi-VN")}`
}

const getCurrentDate = () => {
  const now = new Date()
  const day = String(now.getDate()).padStart(2, "0")
  const month = String(now.getMonth() + 1).padStart(2, "0")
  const year = now.getFullYear()
  return `${day}/${month}/${year}`
}

const getYesterdayDate = () => {
  const yesterday = new Date()
  yesterday.setDate(yesterday.getDate() - 1)
  const day = String(yesterday.getDate()).padStart(2, "0")
  const month = String(yesterday.getMonth() + 1).padStart(2, "0")
  const year = yesterday.getFullYear()
  return `${day}/${month}/${year}`
}

const getCurrentTime = () => {
  const now = new Date()
  const hours = String(now.getHours()).padStart(2, "0")
  const minutes = String(now.getMinutes()).padStart(2, "0")
  const day = String(now.getDate()).padStart(2, "0")
  const month = String(now.getMonth() + 1).padStart(2, "0")
  const year = now.getFullYear()
  return `${hours}:${minutes} (${day}/${month}/${year})`
}

export default function GoldPricePage() {
  const [tableData, setTableData] = useState<PriceData[]>([])
  const [chartData, setChartData] = useState<ChartData[]>([])
  const [selectedDate, setSelectedDate] = useState<string>("")
  const [chartMeta, setChartMeta] = useState<{ brand: string; region?: string | null } | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = async () => {
      try {
        const tableRes = await fetch("/api/prices/latest-table")
        if (!tableRes.ok) {
          throw new Error(`Failed to load table data (${tableRes.status})`)
        }
        const table = (await tableRes.json()) as PriceData[]
        setTableData(table)

        const defaultRow = table.find((row) => row.brand && row.buyToday !== null) ?? table.find((row) => row.brand)
        if (defaultRow?.brand) {
          const params = new URLSearchParams({ brand: defaultRow.brand })
          if (defaultRow.region) {
            params.set("region", defaultRow.region)
          }
          const chartRes = await fetch(`/api/prices/history-30d?${params.toString()}`)
          if (!chartRes.ok) {
            throw new Error(`Failed to load chart data (${chartRes.status})`)
          }
          const historyPayload = (await chartRes.json()) as HistoryResponse
          setChartData(historyPayload.history ?? [])
          setChartMeta({ brand: historyPayload.brand, region: historyPayload.region })
        } else {
          setChartData([])
          setChartMeta(null)
        }
      } catch (error) {
        console.error("Failed to fetch dashboard data", error)
        setError("Lỗi khi tải dữ liệu")
      } finally {
        setLoading(false)
      }
    }

    load()
    setSelectedDate(getCurrentDate())
  }, [])

  const topSummaries = useMemo(() => {
    if (tableData.length === 0) return []

    const prioritizedLabels = ["SJC", "PNJ", "DOJI HN"]
    const hasData = tableData.filter((row) => row.buyToday !== null && row.sellToday !== null)
    const noData = tableData.filter((row) => row.buyToday === null || row.sellToday === null)

    const ordered: PriceData[] = []

    for (const label of prioritizedLabels) {
      const match = hasData.find((row) => row.label === label)
      if (match && !ordered.includes(match)) {
        ordered.push(match)
      }
    }

    for (const row of hasData) {
      if (!ordered.includes(row)) {
        ordered.push(row)
      }
    }

    for (const label of prioritizedLabels) {
      const match = noData.find((row) => row.label === label)
      if (match && !ordered.includes(match)) {
        ordered.push(match)
      }
    }

    for (const row of noData) {
      if (!ordered.includes(row)) {
        ordered.push(row)
      }
    }

    return ordered.slice(0, 2)
  }, [tableData])

  if (error) {
    return <div className="p-8 text-center text-red-500">Lỗi: {error}</div>
  }

  if (loading) {
    return <div className="p-8 text-center">Đang tải dữ liệu...</div>
  }

  return (
    <div className="min-h-screen bg-gray-50 p-4 md:p-8">
      <div className="mb-8">
        <Card>
          <CardContent className="pt-6">
            <div className="text-sm font-semibold text-orange-500 mb-4">GIÁ VÀNG</div>
            <div className="space-y-3">
              {topSummaries.map((row) => (
                <div key={row.label} className="flex justify-between text-xs">
                  <div className="flex gap-8">
                    <div>
                      <div className="text-gray-600">{row.label} Mua</div>
                      <div className="font-semibold text-base">{formatNumber(row.buyToday)}</div>
                    </div>
                    <div>
                      <div className="text-gray-600">Bán</div>
                      <div className="font-semibold text-base">{formatNumber(row.sellToday)}</div>
                    </div>
                  </div>
                </div>
              ))}
              {topSummaries.length === 0 && (
                <div className="text-sm text-gray-500">Không có dữ liệu</div>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="mb-6">
        <div className="flex items-center justify-between mb-4">
          <h1 className="text-3xl font-bold text-orange-500">GIÁ VÀNG</h1>
          <Button variant="default" className="bg-green-600 hover:bg-green-700">
            ✓ CHIA SẺ
          </Button>
        </div>
        <p className="text-xs text-gray-600">Nguồn: giavang.doji.vn - Cập nhật lúc {getCurrentTime()}</p>
      </div>

      <div className="mb-8 bg-white rounded-lg shadow-md p-4">
        <div className="flex gap-4 items-center mb-6">
          <label className="text-sm font-medium text-gray-700">Chọn ngày:</label>
          <input
            type="date"
            value={selectedDate.split("/").reverse().join("-")}
            onChange={(e) => {
              const parts = e.target.value.split("-")
              setSelectedDate(`${parts[2]}/${parts[1]}/${parts[0]}`)
            }}
            className="px-3 py-2 border border-gray-300 rounded-md text-sm"
          />
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-200 border-b">
                <th className="px-4 py-3 text-left font-semibold w-24">Cửa hàng</th>
                <th colSpan={2} className="px-4 py-3 text-center font-semibold">
                  Hôm nay ({getCurrentDate()})
                </th>
                <th colSpan={2} className="px-4 py-3 text-center font-semibold">
                  Hôm qua ({getYesterdayDate()})
                </th>
              </tr>
              <tr className="bg-gray-100 border-b">
                <th className="px-4 py-2 text-left text-xs font-semibold">Cửa hàng</th>
                <th className="px-4 py-2 text-center text-xs font-semibold">Giá mua</th>
                <th className="px-4 py-2 text-center text-xs font-semibold">Giá bán</th>
                <th className="px-4 py-2 text-center text-xs font-semibold">Giá mua</th>
                <th className="px-4 py-2 text-center text-xs font-semibold">Giá bán</th>
              </tr>
            </thead>
            <tbody>
              {tableData.length > 0 ? (
                tableData.map((row) => (
                  <tr key={row.label} className="border-b hover:bg-gray-50">
                    <td className="px-4 py-3 font-semibold text-sm">
                      {row.label}
                      {row.region ? ` (${row.region})` : ""}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <div className="font-semibold">{formatNumber(row.buyToday)}</div>
                      <span
                        className={`text-xs ${
                          row.buyChange === null || row.buyChange === undefined
                            ? "text-gray-400"
                            : row.buyChange >= 0
                            ? "text-green-600"
                            : "text-red-600"
                        }`}
                      >
                        {trendLabel(row.buyChange)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-center">
                      <div className="font-semibold">{formatNumber(row.sellToday)}</div>
                      <span
                        className={`text-xs ${
                          row.sellChange === null || row.sellChange === undefined
                            ? "text-gray-400"
                            : row.sellChange >= 0
                            ? "text-green-600"
                            : "text-red-600"
                        }`}
                      >
                        {trendLabel(row.sellChange)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-center font-semibold">{formatNumber(row.buyYesterday)}</td>
                    <td className="px-4 py-3 text-center font-semibold">{formatNumber(row.sellYesterday)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5} className="px-4 py-3 text-center text-gray-500">
                    Không có dữ liệu
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="bg-white rounded-lg shadow-md">
        <Card>
          <CardHeader>
            <CardTitle>Biểu đồ giá vàng 30 ngày gần nhất</CardTitle>
            <CardDescription>
              Đơn vị: nghìn đồng/lượng
              {chartMeta?.brand ? ` · ${chartMeta.brand}${chartMeta.region ? ` (${chartMeta.region})` : ""}` : ""}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {chartData.length > 0 ? (
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" />
                  <XAxis dataKey="date" tick={{ fontSize: 12 }} tickFormatter={(value) => value.split("-")[2]} />
                  <YAxis domain={["dataMin - 1000", "dataMax + 1000"]} tick={{ fontSize: 12 }} />
                  <Tooltip
                    formatter={(value: number) => value.toLocaleString("vi-VN")}
                    labelFormatter={(label) => `Ngày ${label}`}
                  />
                  <Legend />
                  <Line type="monotone" dataKey="buy" stroke="#ef4444" dot={false} name="Mua vào" strokeWidth={2} />
                  <Line type="monotone" dataKey="sell" stroke="#22c55e" dot={false} name="Bán ra" strokeWidth={2} />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="text-center py-8 text-gray-500">Không có dữ liệu biểu đồ</div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
