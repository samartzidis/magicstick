import type { MagicStickDeviceInfo } from "../types";

interface DeviceSelectorProps {
  devices: MagicStickDeviceInfo[];
  selectedSerial: string;
  onSelectDevice: (serial: string) => void;
  onScan: () => void;
  loading: boolean;
}

export function DeviceSelector({
  devices,
  selectedSerial,
  onSelectDevice,
  onScan,
  loading,
}: DeviceSelectorProps) {
  return (
    <div className="flex flex-col gap-2">
      <label
        htmlFor="device-select"
        className="font-semibold text-sm text-gray-500 dark:text-gray-400 uppercase tracking-wide"
      >
        Device
      </label>
      <div className="flex gap-2 items-center">
        <select
          id="device-select"
          className="min-w-[280px] px-3 py-2 text-[0.95rem] rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-[#333] text-inherit"
          value={selectedSerial}
          onChange={(e) => onSelectDevice(e.target.value)}
          disabled={loading}
        >
          <option value="">Select a device...</option>
          {devices.map((d) => (
            <option key={d.serialNumber} value={d.serialNumber}>
              {d.serialNumber}
            </option>
          ))}
        </select>
        <button
          type="button"
          className="px-4 py-2 text-[0.95rem] rounded-md border border-indigo-500 bg-indigo-500 text-white cursor-pointer whitespace-nowrap hover:bg-indigo-600 disabled:opacity-60 disabled:cursor-not-allowed"
          onClick={onScan}
          disabled={loading}
        >
          {loading ? "Scanning\u2026" : "Scan devices"}
        </button>
      </div>
    </div>
  );
}
