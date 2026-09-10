interface StepperProps {
  steps: string[];
  current: number; // índice 0-based del paso activo
}

/** Barra de progreso del flujo público (mobile-first). */
export default function Stepper({ steps, current }: StepperProps) {
  const pct = Math.round(((current + 1) / steps.length) * 100);
  return (
    <div className="mb-5">
      <div className="mb-2 flex items-center justify-between text-xs font-medium text-muted">
        <span>
          Paso {current + 1} de {steps.length}
        </span>
        <span>{steps[current]}</span>
      </div>
      <div
        className="h-2 w-full overflow-hidden rounded-full bg-line"
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={`Progreso: ${pct}%`}
      >
        <div
          className="h-full rounded-full bg-brand-500 transition-all duration-300"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}
