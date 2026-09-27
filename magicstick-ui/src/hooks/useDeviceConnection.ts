import { useState, useCallback, useEffect, useRef } from "react";
import * as DeviceManagerService from "../bindings/DeviceManagerService";
import { onBatteryReport, onKeyboardConnected, onKeyboardDisconnected, onDeviceDisconnected } from "../bindings/events";
import type { MagicStickDeviceDetails, KeyboardStatus } from "../types";

// Polling intervals (matching legacy magicstick-ui)
const POLL_FAST_MS = 3000; // when we don't have battery data yet
const POLL_SLOW_MS = 60000; // once battery data is available
const RECONNECT_MS = 3000; // auto-reconnect attempt interval

/**
 * Manages the connection lifecycle for the selected device:
 * opening, event subscriptions, battery polling, and auto-reconnect.
 */
export function useDeviceConnection(selectedSerial: string) {
  const [details, setDetails] = useState<MagicStickDeviceDetails | null>(null);
  const [batteryDetails, setBatteryDetails] = useState<MagicStickDeviceDetails | null>(null);
  const [isDeviceOpened, setIsDeviceOpened] = useState(false);
  const [keyboardStatus, setKeyboardStatus] = useState<KeyboardStatus>("unknown");

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const reconnectRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const hasBatteryRef = useRef(false);
  const selectedSerialRef = useRef("");
  const isDeviceOpenedRef = useRef(false);

  // Keep refs in sync with state
  useEffect(() => {
    hasBatteryRef.current = batteryDetails?.batteryLevel != null;
  }, [batteryDetails]);

  useEffect(() => {
    selectedSerialRef.current = selectedSerial;
  }, [selectedSerial]);

  useEffect(() => {
    isDeviceOpenedRef.current = isDeviceOpened;
  }, [isDeviceOpened]);

  // Polling helpers

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
  }, []);

  const stopReconnect = useCallback(() => {
    if (reconnectRef.current) {
      clearInterval(reconnectRef.current);
      reconnectRef.current = null;
    }
  }, []);

  const startPolling = useCallback(() => {
    stopPolling();
    const interval = hasBatteryRef.current ? POLL_SLOW_MS : POLL_FAST_MS;
    const poll = () => {
      DeviceManagerService.RequestBatteryReport().catch(() => {});
    };
    poll();
    pollRef.current = setInterval(poll, interval);
  }, [stopPolling]);

  const startReconnect = useCallback(
    (serial: string) => {
      stopReconnect();
      stopPolling();

      reconnectRef.current = setInterval(async () => {
        if (selectedSerialRef.current !== serial || isDeviceOpenedRef.current) {
          stopReconnect();
          return;
        }
        try {
          await DeviceManagerService.ScanDevices();
          const list = await DeviceManagerService.GetDevices();
          if (!list.some((d) => d.serialNumber === serial)) return;

          const result = await DeviceManagerService.SelectDevice(serial);
          if (result && selectedSerialRef.current === serial) {
            setDetails(result);
            setIsDeviceOpened(true);
            setKeyboardStatus("unknown");
            stopReconnect();
            startPolling();
          }
        } catch {
          // Device not ready yet, will retry
        }
      }, RECONNECT_MS);
    },
    [stopReconnect, stopPolling, startPolling]
  );

  // Subscribe to battery report events
  useEffect(() => {
    const unsub = onBatteryReport((data) => {
      if (data && data.serial === selectedSerialRef.current) {
        setBatteryDetails(data);
        setKeyboardStatus("connected");
      }
    });
    return unsub;
  }, []);

  // Subscribe to keyboard connected/disconnected RPC events
  useEffect(() => {
    const unsubDisconnected = onKeyboardDisconnected((data) => {
      if (data && data.serial === selectedSerialRef.current) {
        setKeyboardStatus("disconnected");
        setBatteryDetails(null);
      }
    });
    const unsubConnected = onKeyboardConnected((data) => {
      if (data && data.serial === selectedSerialRef.current) {
        setKeyboardStatus("connected");
        DeviceManagerService.RequestBatteryReport().catch(() => {});
      }
    });
    return () => {
      unsubDisconnected();
      unsubConnected();
    };
  }, []);

  // Subscribe to device (dongle) disconnected events
  useEffect(() => {
    const unsub = onDeviceDisconnected((data) => {
      if (data && data.serial === selectedSerialRef.current) {
        setIsDeviceOpened(false);
        setKeyboardStatus("unknown");
        setBatteryDetails(null);
        stopPolling();
        startReconnect(data.serial);
      }
    });
    return unsub;
  }, [stopPolling, startReconnect]);

  // Stop reconnect when device opens successfully
  useEffect(() => {
    if (isDeviceOpened && reconnectRef.current) {
      stopReconnect();
    }
  }, [isDeviceOpened, stopReconnect]);

  // Adjust poll interval when battery data changes
  useEffect(() => {
    if (!selectedSerial || !isDeviceOpened) return;
    startPolling();
  }, [batteryDetails?.batteryLevel != null, selectedSerial, isDeviceOpened]);

  // Connect when selected device changes
  useEffect(() => {
    setDetails(null);
    setBatteryDetails(null);
    setIsDeviceOpened(false);
    setKeyboardStatus("unknown");
    stopPolling();
    stopReconnect();

    if (!selectedSerial) return;

    let cancelled = false;

    (async () => {
      try {
        const initial = await DeviceManagerService.SelectDevice(selectedSerial);
        if (!cancelled && initial) {
          setDetails(initial);
          setIsDeviceOpened(true);
        }
      } catch {
        // Device may not be available
      }
      if (!cancelled && isDeviceOpenedRef.current) {
        startPolling();
      }
    })();

    return () => {
      cancelled = true;
      stopPolling();
      stopReconnect();
    };
  }, [selectedSerial]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      stopPolling();
      stopReconnect();
    };
  }, [stopPolling, stopReconnect]);

  // Compute display-ready details by merging base info with live state
  const displayDetails: MagicStickDeviceDetails | null = details
    ? {
        ...details,
        dongleStatus: isDeviceOpened ? "Connected" : "Disconnected",
        keyboardStatus: keyboardStatus === "connected" ? "Connected" : "Disconnected",
        ...(batteryDetails
          ? {
              batteryLevel: batteryDetails.batteryLevel,
              batteryStatus: batteryDetails.batteryStatus,
            }
          : {}),
      }
    : null;

  return {
    details,
    batteryDetails,
    isDeviceOpened,
    keyboardStatus,
    displayDetails,
  };
}
