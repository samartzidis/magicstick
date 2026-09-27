import { useState } from "react";
import { useDeviceManager } from "./hooks/useDeviceManager";
import { useDeviceConnection } from "./hooks/useDeviceConnection";
import { useTrayIcon } from "./hooks/useTrayIcon";
import { DeviceSelector } from "./components/DeviceSelector";
import { DeviceInfoPage } from "./pages/DeviceInfoPage";
import { SettingsPage } from "./pages/SettingsPage";
import { KeymapPage } from "./pages/KeymapPage";
import { AboutPage } from "./pages/AboutPage";
import appIcon from "./assets/app.png";

type Page = "device-info" | "settings" | "keymap" | "about";

const tabs: { id: Page; label: string }[] = [
  { id: "device-info", label: "Device Info" },
  { id: "settings", label: "Settings" },
  { id: "keymap", label: "Keymap" },
  { id: "about", label: "About" },
];

function App() {
  const { devices, selectedSerial, selectDevice, loading, error, scanDevices } =
    useDeviceManager();

  const { batteryDetails, isDeviceOpened, keyboardStatus, displayDetails } =
    useDeviceConnection(selectedSerial);

  useTrayIcon(selectedSerial, isDeviceOpened, keyboardStatus, batteryDetails);

  const [currentPage, setCurrentPage] = useState<Page>("device-info");

  return (
    <div className="flex flex-col flex-1 min-h-0">
      {/* Frozen header: logo, title, device selector */}
      <div className="flex flex-col gap-4 shrink-0">
        <div className="flex items-center gap-3">
          <img src={appIcon} alt="" className="w-14 h-14 shrink-0" aria-hidden />
          <div>
            <h1 className="text-3xl font-semibold leading-tight m-0 mb-1">MagicStick</h1>
            <p className="text-gray-500 dark:text-gray-400 m-0 text-sm">
              UI utility for MagicStick devices.
            </p>
          </div>
        </div>

        {error && (
          <div className="text-red-700 dark:text-red-400 px-4 py-2 bg-red-50 dark:bg-red-900/20 rounded-md">
            {error}
          </div>
        )}

        <DeviceSelector
          devices={devices}
          selectedSerial={selectedSerial}
          onSelectDevice={selectDevice}
          onScan={scanDevices}
          loading={loading}
        />
      </div>

      {/* Tab bar and content always visible so About (and Settings app options) are reachable without a device */}
      <div className="flex flex-col flex-1 min-h-0">
        <nav className="flex gap-0 border-b border-gray-200 dark:border-gray-700 mt-2 shrink-0">
          {tabs.map((tab) => {
            const active = currentPage === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setCurrentPage(tab.id)}
                className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
                  active
                    ? "border-indigo-500 text-indigo-600 dark:text-indigo-400"
                    : "border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300 hover:border-gray-300 dark:hover:border-gray-600"
                }`}
              >
                {tab.label}
              </button>
            );
          })}
        </nav>

        <div className="flex flex-col flex-1 min-h-0 overflow-auto mt-4 pr-2">
          {currentPage === "device-info" && (
            <DeviceInfoPage
              displayDetails={displayDetails}
              isConnecting={!!selectedSerial && !displayDetails}
            />
          )}
          {currentPage === "settings" && <SettingsPage isDeviceOpened={isDeviceOpened} />}
          {currentPage === "keymap" && <KeymapPage isDeviceOpened={isDeviceOpened} />}
          {currentPage === "about" && <AboutPage />}
        </div>
      </div>
    </div>
  );
}

export default App;
