import * as Log from "../../bindings/github.com/wailsapp/wails/v3/pkg/services/log/logservice";
import { useEffect, useRef, useState } from 'react';
import * as DeviceService from "../../bindings/magicstick-ui/deviceservice";
import * as AppService from "../../bindings/magicstick-ui/appservice";
import { GetSettingsReply, SetSettingsRequest } from "../../bindings/magicstick-ui/hid/models";

// Placeholder types - will be replaced with generated types
interface DeviceInfo {
    Index: number;
    VendorID: number;
    ProductID: number;
    Product: string;
    Serial: string;
    Manufacturer: string;
}

// Settings Page Component
interface SettingsPageProps {
    selectedDevice: DeviceInfo | null;
    isDeviceOpened: boolean;
}

export function SettingsPage({ selectedDevice, isDeviceOpened }: SettingsPageProps) {
    const [settings, setSettings] = useState<GetSettingsReply | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isSavingConfig, setIsSavingConfig] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [info, setInfo] = useState<string | null>(null);
    const [autostartEnabled, setAutostartEnabled] = useState(false);
    const [isWindows, setIsWindows] = useState(false);
    const isLoadingRef = useRef(false);

    const loadSettings = async () => {
        if (!selectedDevice || !isDeviceOpened) return;

        // Immediate synchronous check to prevent duplicate calls
        if (isLoadingRef.current) {
            Log.Debug('[SettingsPage] loadSettings already in progress, skipping');
            return;
        }

        Log.Debug('[SettingsPage] loadSettings called');
        isLoadingRef.current = true;
        setIsLoading(true);
        setError(null);
        setInfo(null);
        try {
            Log.Debug('Loading settings for device:', selectedDevice.Serial);

            // Add a small delay to ensure RPC connection is ready
            await new Promise(resolve => setTimeout(resolve, 1000));

            const deviceSettings = await DeviceService.GetSettings();
            setSettings(deviceSettings);
            setError(null); // Clear any previous errors on successful load
            setInfo(null); // Clear any previous info messages on successful load
            Log.Debug('Settings loaded:', deviceSettings);
        } catch (error) {
            Log.Error('Failed to load settings:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);

            setError(`Failed to load settings: ${errorMessage}`);
        } finally {
            isLoadingRef.current = false;
            setIsLoading(false);
        }
    };

    const saveSettings = async () => {
        if (!selectedDevice || !isDeviceOpened || !settings) return;

        setIsSaving(true);
        setError(null);
        try {
            Log.Debug('Saving settings for device:', selectedDevice.Serial, settings);

            // Create SetSettingsRequest from current settings
            // Use the generated type from bindings
            const request = new SetSettingsRequest({
                method_name: "",
                id: "",
                swap_fn_ctrl: settings.swap_fn_ctrl,
                swap_alt_cmd: settings.swap_alt_cmd,
                bluetooth_disabled: settings.bluetooth_disabled,
                io_timing: settings.io_timing
            });

            await DeviceService.SetSettings(request);
            Log.Info('Settings saved successfully');
        } catch (error) {
            Log.Error('Failed to save settings:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError(`Failed to save settings: ${errorMessage}`);
        } finally {
            setIsSaving(false);
        }
    };

    const handleSettingChange = (key: keyof GetSettingsReply, value: boolean | number) => {
        if (!settings) return;

        setSettings({
            ...settings,
            [key]: value
        });
    };

    const saveConfig = async () => {
        if (!selectedDevice || !isDeviceOpened) return;

        setIsSavingConfig(true);
        setError(null);
        try {
            Log.Debug('Saving config for device:', selectedDevice.Serial);
            await DeviceService.SaveConfig();
            Log.Info('Config saved successfully');
        } catch (error) {
            Log.Error('Failed to save config:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError(`Failed to save config: ${errorMessage}`);
        } finally {
            setIsSavingConfig(false);
        }
    };

    const handleAutostartChange = async (enabled: boolean) => {
        try {
            await AppService.SetAutostart(enabled);
            setAutostartEnabled(enabled);
            Log.Info(`Autostart ${enabled ? 'enabled' : 'disabled'} successfully`);
        } catch (error) {
            Log.Error('Failed to set autostart:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError(`Failed to set autostart: ${errorMessage}`);
        }
    };

    // Load current autostart status on component mount
    useEffect(() => {
        const loadAutostartStatus = async () => {
            try {
                // Check if running on Windows
                const userAgent = navigator.userAgent.toLowerCase();
                const isWindowsOS = userAgent.includes('windows');
                setIsWindows(isWindowsOS);

                if (isWindowsOS) {
                    const status = await AppService.GetAutostartStatus();
                    setAutostartEnabled(status);
                }
            } catch (error) {
                Log.Error('Failed to load autostart status:', error);
                // Don't show error to user for this, just log it
            }
        };

        loadAutostartStatus();
    }, []);

    // Load settings when Settings tab becomes active
    useEffect(() => {
        Log.Debug('[SettingsPage] useEffect triggered - isDeviceOpened:', isDeviceOpened, 'selectedDevice:', selectedDevice?.Serial, 'isLoadingRef:', isLoadingRef.current, 'settings:', !!settings);
        if (isDeviceOpened && selectedDevice && !settings && !isLoadingRef.current) {
            loadSettings();
        }
    }, [isDeviceOpened, selectedDevice?.Serial]);

    if (!selectedDevice) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">No Device Selected</h2>
                    <p className="text-muted">Please select a device from the dropdown above to manage its settings.</p>
                </div>
            </div>
        );
    }

    if (!isDeviceOpened) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">Device Not Connected</h2>
                    <p className="text-muted">Please connect to the device first to access its settings.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="container-fluid p-4">
            <h1 className="mb-4">Settings</h1>

            {error && (
                <div className="alert alert-danger" role="alert">
                    {error}
                </div>
            )}

            {info && (
                <div className="alert alert-info" role="alert">
                    {info}
                </div>
            )}

            
                <div className="card mt-3">
                    <div className="card-body">
                        <h4 className="card-title">UI Utility</h4>

                        {isWindows && (
                            <div className="form-check mb-3">
                                <input
                                    className="form-check-input"
                                    type="checkbox"
                                    id="autostart-checkbox"
                                    checked={autostartEnabled}
                                    onChange={(e) => handleAutostartChange(e.target.checked)}
                                />
                                <label className="form-check-label" htmlFor="autostart-checkbox">
                                    Automatically start with Windows
                                </label>
                            </div>
                        )}
                       
                    </div>
                </div>
            
                {isLoading ? (
                <div className="text-center p-4">
                    <p className="text-muted">Loading...</p>
                </div>
                    ) : (
                <div>
                    <div className="card mt-3">
                        <div className="card-body">
                            <h4 className="card-title">Device Settings</h4>
                            
                            <div>
                                <div className="mb-4">
                                    <div className="form-check mb-3">
                                        <input
                                            className="form-check-input"
                                            type="checkbox"
                                            id="swap-fn-ctrl"
                                            checked={settings?.swap_fn_ctrl || false}
                                            onChange={(e) => handleSettingChange('swap_fn_ctrl', e.target.checked)}
                                        />
                                        <label className="form-check-label" htmlFor="swap-fn-ctrl">
                                            <strong>Swap Fn and Control</strong>
                                        </label>
                                        <div className="form-text">Swap Fn and Ctrl keys.</div>
                                    </div>

                                    <div className="form-check mb-3">
                                        <input
                                            className="form-check-input"
                                            type="checkbox"
                                            id="swap-alt-cmd"
                                            checked={settings?.swap_alt_cmd || false}
                                            onChange={(e) => handleSettingChange('swap_alt_cmd', e.target.checked)}
                                        />
                                        <label className="form-check-label" htmlFor="swap-alt-cmd">
                                            <strong>Swap Alt-Cmd</strong>
                                        </label>
                                        <div className="form-text">Swap Alt(Option) and Command keys.</div>
                                    </div>

                                    <div className="form-check mb-3">
                                        <input
                                            className="form-check-input"
                                            type="checkbox"
                                            id="bluetooth-disabled"
                                            checked={settings?.bluetooth_disabled || false}
                                            onChange={(e) => handleSettingChange('bluetooth_disabled', e.target.checked)}
                                        />
                                        <label className="form-check-label" htmlFor="bluetooth-disabled">
                                            <strong>Disable Bluetooth</strong>
                                        </label>
                                        <div className="form-text">Only allow wired connection.</div>
                                    </div>

                                    
                                </div>

                                <div className="mb-3">
                                    <label htmlFor="io-timing" className="form-label">
                                        <strong>IO Timing</strong>
                                    </label>
                                    <input
                                        type="number"
                                        className="form-control"
                                        id="io-timing"
                                        min="0"
                                        max="200"
                                        value={settings?.io_timing || 50}
                                        onChange={(e) => handleSettingChange('io_timing', parseInt(e.target.value) || 50)}
                                        style={{ width: '100px' }}
                                    />
                                    <div className="form-text">
                                        Adjusts the internal HID-RPC protocol timing. You should only change that if the UI utility has communication issues. Allowed values are: 0-200.
                                    </div>
                                </div>

                                <div className="d-flex gap-2">
                                    <button
                                        className="btn btn-success"
                                        onClick={saveSettings}
                                        disabled={isSaving || !settings}
                                    >
                                        {isSaving ? 'Applying...' : 'Apply'}
                                    </button>
                                    <button
                                        className="btn btn-secondary"
                                        onClick={loadSettings}
                                        disabled={isLoading}
                                    >
                                        Reload
                                    </button>
                                </div>
                            </div>
                            
                        </div>
                    </div>                
                    <div className="card mt-3">
                        <div className="card-body">
                            <h4 className="card-title">Device Memory</h4>
                            <p className="card-text">Permanently store current device settings and keymap to the device memory.<br/>This will make the current settings permanent into the device memoryand survive unplugging.</p>

                            <div className="d-flex gap-2 mb-3">
                                <button
                                    className="btn btn-info"
                                    onClick={saveConfig}
                                    disabled={isSavingConfig}
                                >
                                    {isSavingConfig ? 'Saving...' : 'Save'}
                                </button>
                            </div>

                        </div>
                    </div>            
                </div>
                )}
        </div>
    );
}

