import { useState } from 'react';
import { hid } from "../../wailsjs/go/models";

// Define BatteryStatus interface locally
interface BatteryStatus {
    percentage: number;
    status: number; // Raw battery status from device
}

// Battery status constants (matching backend)
const BatteryStatusGood = 0x01;  // present/OK
const BatteryStatusCharging = 0x02;  // charging
const BatteryStatusDischarging = 0x04;  // discharging

// Device Info Page Component
interface DeviceInfoPageProps {
    selectedDevice: hid.DeviceInfo | null;
    isDeviceOpened: boolean;
    isOpeningDevice: boolean;
    isKeyboardConnected: boolean;
    batteryStatus: BatteryStatus | null;
    onReconnect?: () => void;
}

export function DeviceInfoPage({ selectedDevice, isDeviceOpened, isOpeningDevice, isKeyboardConnected, batteryStatus, onReconnect }: DeviceInfoPageProps) {
    const [showPaths, setShowPaths] = useState(false);

    if (!selectedDevice) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">No Device Selected</h2>
                    <p className="text-muted">Please select a device from the dropdown above to view its information.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="container-fluid p-4">
            <h1 className="mb-4">Device Information</h1>
            <div className="card mb-4">
                <div className="card-body">
                    <div className="row">
                        <div className="col-md-6">
                            <div className="mb-1"><strong>Product:</strong> {selectedDevice.Product}</div>
                            <div className="mb-1"><strong>Serial:</strong> {selectedDevice.Serial || 'N/A'}</div>
                            <div className="mb-1"><strong>Manufacturer:</strong> {selectedDevice.Manufacturer}</div>
                        </div>
                        <div className="col-md-6">
                            <div className="mb-1"><strong>Dongle Status:</strong>
                                {isOpeningDevice ? ' 🔄 Opening...' :
                                    isDeviceOpened ? ' ✅ Connected' :
                                        <span>
                                            ❌ Disconnected
                                            {onReconnect && (
                                                <button
                                                    className="btn btn-sm btn-outline-primary ms-2"
                                                    onClick={onReconnect}
                                                    disabled={isOpeningDevice}
                                                >
                                                    Reconnect
                                                </button>
                                            )}
                                        </span>}
                            </div>
                            <div className="mb-1"><strong>Keyboard Status:</strong>
                                {isKeyboardConnected ? ' ✅ Connected' :
                                    ' ❌ Disconnected'}
                            </div>
                            <div className="mb-1"><strong>Battery Level:</strong>
                                {isKeyboardConnected && batteryStatus ? ` ${batteryStatus.percentage}%` :
                                    isKeyboardConnected ? 'Unknown' : ''}
                            </div>
                            <div className="mb-1"><strong>Battery Status:</strong>
                                {isKeyboardConnected && batteryStatus ?
                                    (batteryStatus.status & BatteryStatusCharging ? ' 🔌 Charging' :
                                        batteryStatus.status & BatteryStatusGood ? ' 🔋 Good' :
                                            batteryStatus.status & BatteryStatusDischarging ? ' ⚡Discharging' :
                                                ' Unknown') :
                                    isKeyboardConnected ? ' Unknown' : ''}
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div className="card">
                <div className="card-header">
                    <h3 className="mb-0">
                        <button
                            className="btn btn-link text-decoration-none p-0 w-100 text-start"
                            onClick={() => setShowPaths(!showPaths)}
                        >
                            Technical Details
                            <i className={`fas fa-chevron-${showPaths ? 'up' : 'down'} float-end`}></i>
                        </button>
                    </h3>
                </div>
                {showPaths && (
                    <div className="card-body">
                        <div className="row">
                            <div className="col-md-6">
                                <h5>Device Information</h5>
                                <div className="mb-1"><strong>Vendor ID:</strong> 0x{selectedDevice.VendorID.toString(16).toUpperCase()}</div>
                                <div className="mb-1"><strong>Product ID:</strong> 0x{selectedDevice.ProductID.toString(16).toUpperCase()}</div>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
