import { useState, useCallback, useEffect } from "react";
import * as DeviceManagerService from "../bindings/DeviceManagerService";
import type { MagicStickDeviceInfo } from "../types";

const STORAGE_KEY = "lastSelectedDevice";

/**
 * Manages the device list, scanning, selection, and localStorage persistence.
 */
export function useDeviceManager() {
  const [devices, setDevices] = useState<MagicStickDeviceInfo[]>([]);
  const [selectedSerial, setSelectedSerial] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>("");

  const scanDevices = useCallback(async () => {
    try {
      setError("");
      setLoading(true);
      await DeviceManagerService.ScanDevices();
      const list = await DeviceManagerService.GetDevices();
      setDevices(list);

      if (list.length > 0) {
        // Use functional update to avoid stale closure over selectedSerial
        setSelectedSerial((current) => {
          if (list.some((d) => d.serialNumber === current)) return current;
          const lastSerial = localStorage.getItem(STORAGE_KEY);
          const restored = lastSerial && list.find((d) => d.serialNumber === lastSerial);
          return restored ? lastSerial! : list[0].serialNumber;
        });
      } else {
        setSelectedSerial("");
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  const selectDevice = useCallback((serial: string) => {
    setSelectedSerial(serial);
    if (serial) {
      localStorage.setItem(STORAGE_KEY, serial);
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }, []);

  // Load devices on mount
  useEffect(() => {
    scanDevices();
  }, [scanDevices]);

  return { devices, selectedSerial, selectDevice, loading, error, scanDevices };
}
