import { useEffect, useState } from 'react';
import * as Device from "../../wailsjs/go/hid/Device";
import * as AppBackend from "../../wailsjs/go/main/App";
import { hid } from "../../wailsjs/go/models";

// Config Page Component
interface ConfigPageProps {
    selectedDevice: hid.DeviceInfo | null;
    isDeviceOpened: boolean;
}

export function ConfigPage({ selectedDevice, isDeviceOpened }: ConfigPageProps) {
    const [isSavingConfig, setIsSavingConfig] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [autostartEnabled, setAutostartEnabled] = useState(false);
    const [isWindows, setIsWindows] = useState(false);

    const saveConfig = async () => {
        if (!selectedDevice || !isDeviceOpened) return;

        setIsSavingConfig(true);
        setError(null);
        try {
            console.log('Saving config for device:', selectedDevice.Serial);
            await Device.SaveConfig();
            console.log('Config saved successfully');
        } catch (error) {
            console.error('Failed to save config:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError(`Failed to save config: ${errorMessage}`);
        } finally {
            setIsSavingConfig(false);
        }
    };

    const handleAutostartChange = async (enabled: boolean) => {
        try {
            await AppBackend.SetAutostart(enabled);
            setAutostartEnabled(enabled);
            console.log(`Autostart ${enabled ? 'enabled' : 'disabled'} successfully`);
        } catch (error) {
            console.error('Failed to set autostart:', error);
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
                    const status = await AppBackend.GetAutostartStatus();
                    setAutostartEnabled(status);
                }
            } catch (error) {
                console.error('Failed to load autostart status:', error);
                // Don't show error to user for this, just log it
            }
        };

        loadAutostartStatus();
    }, []);

    if (!selectedDevice) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">No Device Selected</h2>
                    <p className="text-muted">Please select a device from the dropdown above to manage its configuration.</p>
                </div>
            </div>
        );
    }

    if (!isDeviceOpened) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">Device Not Connected</h2>
                    <p className="text-muted">Please connect to the device first to access its configuration.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="container-fluid p-4">
            <h1 className="mb-4">Configuration</h1>

            {error && (
                <div className="alert alert-danger" role="alert">
                    {error}
                </div>
            )}

            <div className="card">
                <div className="card-body">
                    <h4 className="card-title">Save Configuration</h4>
                    <p className="card-text">Permanently store current configuration on the MagicStick device. This will make the current settings permanent and survive device restarts.</p>

                    <div className="d-flex gap-2 mb-3">
                        <button
                            className="btn btn-info"
                            onClick={saveConfig}
                            disabled={isSavingConfig}
                        >
                            {isSavingConfig ? 'Saving...' : 'Save Config'}
                        </button>
                    </div>

                    {isWindows && (
                        <div className="card mt-3">
                            <div className="card-body">
                                <div className="form-check">
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
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
