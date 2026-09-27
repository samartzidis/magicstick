export function DetailRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline gap-2">
      <strong className="text-sm text-gray-500 dark:text-gray-400 whitespace-nowrap min-w-[120px] shrink-0">
        {label}
      </strong>
      <span className="text-[0.95rem]">{children}</span>
    </div>
  );
}
