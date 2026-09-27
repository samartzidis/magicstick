// Re-export model types from auto-generated bindings
export type {
  MagicStickDeviceInfo,
  MagicStickDeviceDetails,
  DeviceSettings,
  VersionInfo,
} from "../bindings/models";

/** Keyboard connection status tristate used for tray icon logic. */
export type KeyboardStatus = "unknown" | "connected" | "disconnected";
