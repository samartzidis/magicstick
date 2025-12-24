import { BatteryReportEvent } from "../../bindings/magicstick-ui/models";
import { parseBatteryStatus } from "../batteryStatus";

// Placeholder type - will be replaced with generated type
interface DeviceInfo {
    Index: number;
    VendorID: number;
    ProductID: number;
    Product: string;
    Serial: string;
    Manufacturer: string;
}

// Device Info Page Component
interface DeviceInfoPageProps {
    selectedDevice: DeviceInfo | null;
    isDeviceOpened: boolean;
    isOpeningDevice: boolean;
    batteryStatus: BatteryReportEvent | null;
}

export function DeviceInfoPage({ selectedDevice, isDeviceOpened, isOpeningDevice, batteryStatus }: DeviceInfoPageProps) {
    const isKeyboardConnected = batteryStatus !== null;
    const battery = batteryStatus ? parseBatteryStatus(batteryStatus) : null;

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
                                    isDeviceOpened ? ' ✅ Connected' : ' ❌ Disconnected'}
                            </div>
                            <div className="mb-1"><strong>Keyboard Status:</strong>
                                {isKeyboardConnected ? ' ✅ Connected' :
                                    ' ❌ Disconnected'}
                            </div>
                            <div className="mb-1"><strong>Battery Level:</strong>
                                {battery ? ` ${battery.level}%` : ''}
                            </div>
                            <div className="mb-1"><strong>Battery Status:</strong>
                                {battery ? ` ${battery.statusText}` : ''}
                            </div>
                        </div>
                    </div>
                </div>
            </div>            
        </div>
    );
}

