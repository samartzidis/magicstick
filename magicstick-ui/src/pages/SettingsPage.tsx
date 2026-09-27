import { useState, useEffect, useRef, useCallback } from "react";
import { isEnabled as isAutostartEnabled, enable as enableAutostart, disable as disableAutostart } from "@tauri-apps/plugin-autostart";
import * as AppService from "../bindings/AppService";
import * as DeviceManagerService from "../bindings/DeviceManagerService";
import type { DeviceSettings } from "../types";

interface SettingsPageProps {
  isDeviceOpened: boolean;
}

function Checkbox({
  id,
  label,
  description,
  checked,
  onChange,
  disabled,
}: {
  id: string;
  label: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex items-start gap-3">
      <input
        type="checkbox"
        id={id}
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        disabled={disabled}
        className="mt-1 h-4 w-4 rounded border-gray-300 dark:border-gray-600 accent-indigo-500"
      />
      <div>
        <label htmlFor={id} className="font-medium text-sm cursor-pointer">
          {label}
        </label>
        {description && (
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5 m-0">{description}</p>
        )}
      </div>
    </div>
  );
}

export function SettingsPage({ isDeviceOpened }: SettingsPageProps) {
  const [isWindows, setIsWindows] = useState(false);
  const [autoStart, setAutoStart] = useState(false);
  const [startMinimized, setStartMinimized] = useState(false);
  const [settings, setSettings] = useState<DeviceSettings | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isSavingConfig, setIsSavingConfig] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const isLoadingRef = useRef(false);

  const loadSettings = useCallback(async () => {
    if (!isDeviceOpened || isLoadingRef.current) return;

    isLoadingRef.current = true;
    setIsLoading(true);
    setError(null);
    setInfo(null);

    try {
      // Small delay to ensure RPC connection is ready
      await new Promise((resolve) => setTimeout(resolve, 500));
      const result = await DeviceManagerService.GetSettings();
      setSettings(result);
    } catch (e) {
      setError(`Failed to load settings: ${(e as Error).message}`);
    } finally {
      isLoadingRef.current = false;
      setIsLoading(false);
    }
  }, [isDeviceOpened]);

  // Detect Windows so we can show/hide the "Start with Windows" section
  useEffect(() => {
    AppService.IsWindows().then(setIsWindows).catch(() => {});
  }, []);

  // Load app auto-start and start-minimized on mount (Windows only)
  useEffect(() => {
    if (!isWindows) return;
    isAutostartEnabled().then(setAutoStart).catch(() => {});
    AppService.GetStartMinimized().then(setStartMinimized).catch(() => {});
  }, [isWindows]);

  // Load device settings when page mounts and device is opened
  useEffect(() => {
    if (isDeviceOpened && !settings && !isLoadingRef.current) {
      loadSettings();
    }
  }, [isDeviceOpened, settings, loadSettings]);

  const handleAutoStartChange = useCallback(async (checked: boolean) => {
    try {
      if (checked) {
        await enableAutostart();
      } else {
        await disableAutostart();
      }
      setAutoStart(checked);
    } catch {
      setError("Failed to update start with Windows setting.");
    }
  }, []);

  const handleStartMinimizedChange = useCallback(async (checked: boolean) => {
    try {
      await AppService.SetStartMinimized(checked);
      setStartMinimized(checked);
    } catch {
      setError("Failed to update start minimized setting.");
    }
  }, []);

  const applySettings = useCallback(async () => {
    if (!settings || !isDeviceOpened) return;

    setIsSaving(true);
    setError(null);
    setInfo(null);

    try {
      await DeviceManagerService.ApplySettings(settings);
      setInfo("Settings applied successfully.");
    } catch (e) {
      setError(`Failed to apply settings: ${(e as Error).message}`);
    } finally {
      setIsSaving(false);
    }
  }, [settings, isDeviceOpened]);

  const saveConfig = useCallback(async () => {
    if (!isDeviceOpened) return;

    setIsSavingConfig(true);
    setError(null);
    setInfo(null);

    try {
      await DeviceManagerService.SaveConfig();
      setInfo("Configuration saved to device memory.");
    } catch (e) {
      setError(`Failed to save configuration: ${(e as Error).message}`);
    } finally {
      setIsSavingConfig(false);
    }
  }, [isDeviceOpened]);

  const updateSetting = useCallback(
    <K extends keyof DeviceSettings>(key: K, value: DeviceSettings[K]) => {
      setSettings((prev) => (prev ? { ...prev, [key]: value } : null));
    },
    []
  );

  return (
    <div className="flex flex-col gap-4">
      {/* Error / Info banners */}
      {error && (
        <div className="text-red-700 dark:text-red-400 px-4 py-2 bg-red-50 dark:bg-red-900/20 rounded-md text-sm">
          {error}
        </div>
      )}
      {info && (
        <div className="text-green-700 dark:text-green-400 px-4 py-2 bg-green-50 dark:bg-green-900/20 rounded-md text-sm">
          {info}
        </div>
      )}

      {/* Application (Windows only) */}
      {isWindows && (
        <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
          <h3 className="text-base font-semibold mb-4 m-0">Application</h3>
          <Checkbox
            id="auto-start"
            label="Automatically start with Windows"
            description="Launch MagicStick when you sign in to Windows."
            checked={autoStart}
            onChange={handleAutoStartChange}
          />
          <Checkbox
            id="start-minimized"
            label="Start minimized"
            description="Hide the window at startup. Open it from the tray icon."
            checked={startMinimized}
            onChange={handleStartMinimizedChange}
          />
        </div>
      )}

      {/* Device Settings - only when device is opened */}
      {!isDeviceOpened ? (
        <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
          <p className="text-gray-500 dark:text-gray-400 m-0">
            Device not connected. Connect to a device to manage settings.
          </p>
        </div>
      ) : isLoading ? (
        <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
          <p className="text-gray-500 dark:text-gray-400 m-0">Loading settings{"…"}</p>
        </div>
      ) : (
        <>
      {/* Device Settings */}
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <h3 className="text-base font-semibold mb-4 m-0">Device Settings</h3>

        {settings ? (
          <div className="flex flex-col gap-5">
            <div className="flex flex-col gap-4">
              <Checkbox
                id="swap-fn-ctrl"
                label="Swap Fn and Control"
                description="Swap Fn and Ctrl keys."
                checked={settings.swapFnCtrl}
                onChange={(v) => updateSetting("swapFnCtrl", v)}
                disabled={isSaving}
              />
              <Checkbox
                id="swap-alt-cmd"
                label="Swap Alt-Cmd"
                description="Swap Alt (Option) and Command keys."
                checked={settings.swapAltCmd}
                onChange={(v) => updateSetting("swapAltCmd", v)}
                disabled={isSaving}
              />
              <Checkbox
                id="bluetooth-disabled"
                label="Disable Bluetooth"
                description="Only allow wired connection."
                checked={settings.bluetoothDisabled}
                onChange={(v) => updateSetting("bluetoothDisabled", v)}
                disabled={isSaving}
              />
            </div>

            <div>
              <label htmlFor="io-timing" className="block font-medium text-sm mb-1">
                IO Timing
              </label>
              <input
                type="number"
                id="io-timing"
                min={0}
                max={200}
                value={settings.ioTiming}
                onChange={(e) =>
                  updateSetting("ioTiming", Math.max(0, Math.min(200, parseInt(e.target.value) || 0)))
                }
                disabled={isSaving}
                className="w-20 px-2 py-1.5 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-[#333] text-inherit"
              />
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-1 m-0">
                Adjusts the internal HID-RPC protocol timing. Only change if the UI has
                communication issues. Allowed values: 0-200.
              </p>
            </div>

            <div className="flex gap-2 pt-1">
              <button
                type="button"
                onClick={applySettings}
                disabled={isSaving}
                className="px-4 py-2 text-sm rounded-md border border-indigo-500 bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                {isSaving ? "Applying…" : "Apply"}
              </button>
              <button
                type="button"
                onClick={() => {
                  setSettings(null);
                  loadSettings();
                }}
                disabled={isLoading}
                className="px-4 py-2 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-transparent text-inherit cursor-pointer hover:bg-gray-100 dark:hover:bg-white/10 disabled:opacity-60 disabled:cursor-not-allowed"
              >
                Reload
              </button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-start gap-3">
            <p className="text-gray-500 dark:text-gray-400 m-0">
              No settings data available.
            </p>
            <button
              type="button"
              onClick={loadSettings}
              disabled={isLoading}
              className="px-4 py-2 text-sm rounded-md border border-gray-300 dark:border-gray-600 bg-transparent text-inherit cursor-pointer hover:bg-gray-100 dark:hover:bg-white/10 disabled:opacity-60 disabled:cursor-not-allowed"
            >
              Reload Settings
            </button>
          </div>
        )}
      </div>

      {/* Device Memory */}
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <h3 className="text-base font-semibold mb-2 m-0">Device Memory</h3>
        <p className="text-sm text-gray-500 dark:text-gray-400 mt-0 mb-4">
          Permanently store current device settings and keymap to the device memory.
          This makes the current configuration survive unplugging.
        </p>
        <button
          type="button"
          onClick={saveConfig}
          disabled={isSavingConfig}
          className="px-4 py-2 text-sm rounded-md border border-indigo-500 bg-indigo-500 text-white cursor-pointer hover:bg-indigo-600 disabled:opacity-60 disabled:cursor-not-allowed"
        >
          {isSavingConfig ? "Saving…" : "Save"}
        </button>
      </div>
        </>
      )}
    </div>
  );
}
