import {
  Bar,
  BarChart as RechartsBarChart,
  CartesianGrid,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from "recharts";
import { cn } from "@/lib/cn";

export type BarChartItem = {
  label: string;
  value: number;
  tone?: "sun" | "lawn" | "zombie" | "bad" | "ok";
};

const toneFill: Record<NonNullable<BarChartItem["tone"]>, string> = {
  sun: "var(--color-sun)",
  lawn: "var(--color-lawn-hot)",
  zombie: "var(--color-zombie)",
  bad: "var(--color-bad)",
  ok: "var(--color-ok)"
};

export function BarChart({
  items,
  className,
  testId = "bar-chart",
  emptyLabel = "No data"
}: {
  items: BarChartItem[];
  className?: string;
  testId?: string;
  emptyLabel?: string;
}) {
  if (items.length === 0) {
    return (
      <div data-testid={testId} className={cn("text-sm text-muted", className)}>
        {emptyLabel}
      </div>
    );
  }

  const data = items.map((i) => ({
    name: i.label,
    value: i.value,
    fill: toneFill[i.tone ?? (i.value < 0 ? "bad" : "sun")]
  }));

  return (
    <div data-testid={testId} className={cn("h-48 w-full min-w-0", className)}>
      <ResponsiveContainer width="100%" height="100%">
        <RechartsBarChart data={data} layout="vertical" margin={{ top: 4, right: 12, left: 4, bottom: 4 }}>
          <CartesianGrid stroke="var(--color-border)" strokeDasharray="3 3" horizontal={false} />
          <XAxis
            type="number"
            stroke="var(--color-muted)"
            tick={{ fill: "var(--color-muted)", fontSize: 11 }}
          />
          <YAxis
            type="category"
            dataKey="name"
            width={72}
            stroke="var(--color-muted)"
            tick={{ fill: "var(--color-muted)", fontSize: 11 }}
          />
          <Tooltip
            contentStyle={{
              background: "var(--color-panel)",
              border: "1px solid var(--color-border)",
              color: "var(--color-text)"
            }}
            formatter={(value) => {
              const n = typeof value === "number" ? value : Number(value);
              return Number.isFinite(n) ? (n >= 0 ? `+${n.toFixed(0)}` : n.toFixed(0)) : String(value);
            }}
          />
          <Bar dataKey="value" radius={[0, 4, 4, 0]}>
            {data.map((entry) => (
              <Cell key={entry.name} fill={entry.fill} />
            ))}
          </Bar>
        </RechartsBarChart>
      </ResponsiveContainer>
    </div>
  );
}
