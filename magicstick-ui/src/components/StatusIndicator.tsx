export function StatusIndicator({ label, value }: { label: string; value: string }) {
  const isConnected = value === "Connected";
  return (
    <div className="flex items-baseline gap-2">
      <strong className="text-sm text-gray-500 dark:text-gray-400 whitespace-nowrap min-w-[120px] shrink-0">
        {label}
      </strong>
      <span
        className={`inline-flex items-center gap-1 ${
          isConnected
            ? "text-green-700 dark:text-green-500"
            : "text-red-700 dark:text-red-400"
        }`}
      >
        <span className="text-sm font-bold" aria-hidden>
          {isConnected ? "\u2713" : "\u2717"}
        </span>
        {value}
      </span>
    </div>
  );
}
