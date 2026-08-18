import { Area, AreaChart, ResponsiveContainer, Tooltip, YAxis } from "recharts";
import { cn } from "@/lib/cn";

export function Sparkline({
  values,
  className,
  testId = "sparkline",
  height = 48
}: {
  values: number[];
  className?: string;
  testId?: string;
  height?: number;
  width?: number;
}) {
  if (values.length === 0) {
    return (
      <div data-testid={testId} className={cn("text-sm text-muted", className)}>
        No recent XP
      </div>
    );
  }

  const data = values.map((v, i) => ({ i, v }));

  return (
    <div data-testid={testId} className={cn("w-full max-w-md", className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 4, right: 4, left: 4, bottom: 4 }}>
          <YAxis hide domain={["auto", "auto"]} />
          <Tooltip
            contentStyle={{
              background: "var(--color-panel)",
              border: "1px solid var(--color-border)",
              color: "var(--color-text)"
            }}
            labelFormatter={() => "Δ XP"}
            formatter={(value) => {
              const n = typeof value === "number" ? value : Number(value);
              return Number.isFinite(n) ? n.toFixed(0) : String(value);
            }}
          />
          <Area
            type="monotone"
            dataKey="v"
            stroke="var(--color-sun)"
            fill="var(--color-sun)"
            fillOpacity={0.2}
            strokeWidth={2}
            isAnimationActive={false}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
