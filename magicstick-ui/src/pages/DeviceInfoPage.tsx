import { DetailRow } from "../components/DetailRow";
import { StatusIndicator } from "../components/StatusIndicator";
import type { MagicStickDeviceDetails } from "../types";

interface DeviceInfoPageProps {
  displayDetails: MagicStickDeviceDetails | null;
  isConnecting: boolean;
}

export function DeviceInfoPage({ displayDetails, isConnecting }: DeviceInfoPageProps) {
  if (isConnecting) {
    return (
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <p className="text-gray-500 dark:text-gray-400 m-0">Connecting to device{"\u2026"}</p>
      </div>
    );
  }

  if (!displayDetails) return null;

  return (
    <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
      <div className="grid grid-cols-2 gap-y-6 gap-x-12">
        <div className="flex flex-col gap-3">
          <DetailRow label="Product:">{displayDetails.product}</DetailRow>
          <DetailRow label="Serial:">{displayDetails.serial}</DetailRow>
          <DetailRow label="Manufacturer:">{displayDetails.manufacturer}</DetailRow>
        </div>
        <div className="flex flex-col gap-3">
          <StatusIndicator label="Dongle Status:" value={displayDetails.dongleStatus} />
          <StatusIndicator label="Keyboard Status:" value={displayDetails.keyboardStatus} />
          <DetailRow label="Battery Level:">
            {displayDetails.batteryLevel != null
              ? `${displayDetails.batteryLevel}%`
              : "\u2014"}
          </DetailRow>
          <DetailRow label="Battery Status:">
            {displayDetails.batteryStatus || "\u2014"}
          </DetailRow>
        </div>
      </div>
    </div>
  );
}
