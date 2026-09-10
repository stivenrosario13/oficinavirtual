export default function ProgressBar({ percent }: { percent: number }) {
  const pct = Math.max(0, Math.min(100, percent));
  const color =
    pct >= 100 ? "bg-ok" : pct >= 50 ? "bg-brand-500" : "bg-warn";
  return (
    <div className="flex items-center gap-2">
      <div className="h-2 w-full min-w-16 overflow-hidden rounded-full bg-line">
        <div
          className={`h-full rounded-full ${color}`}
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="w-10 text-right text-xs font-semibold text-muted">
        {pct}%
      </span>
    </div>
  );
}
