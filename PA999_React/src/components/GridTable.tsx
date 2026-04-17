import type { GridRow } from '../types/api'

interface Props { rows: GridRow[] }

export function GridTable({ rows }: Props) {
  if (!rows.length) return null

  const columns = Object.keys(rows[0])

  return (
    <div className="mt-3 overflow-x-auto rounded-lg border border-gray-200 shadow-sm">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            {columns.map(col => (
              <th
                key={col}
                className="whitespace-nowrap px-4 py-2 text-left font-semibold text-gray-600 tracking-wide uppercase text-xs"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">
          {rows.map((row, ri) => (
            <tr key={ri} className="hover:bg-blue-50 transition-colors">
              {columns.map(col => (
                <td key={col} className="whitespace-nowrap px-4 py-2 text-gray-700">
                  {formatCell(row[col])}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <div className="bg-gray-50 px-4 py-1.5 text-xs text-gray-400 text-right">
        {rows.length}행
      </div>
    </div>
  )
}

function formatCell(val: unknown): string {
  if (val === null || val === undefined) return '-'
  if (typeof val === 'number') return val.toLocaleString('ko-KR')
  return String(val)
}
