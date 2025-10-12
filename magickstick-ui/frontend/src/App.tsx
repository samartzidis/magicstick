import { useEffect, useState } from 'react';
import * as AppBackend from "../wailsjs/go/main/App";
import * as MagicStickDevice from "../wailsjs/go/main/MagicStickDevice";
import { main } from "../wailsjs/go/models";
import { EventsOn } from "../wailsjs/runtime/runtime";
import './App.css';
import appicon from './assets/images/appicon.png';
import { AboutPage } from './components/AboutPage';
import { ConfigPage } from './components/ConfigPage';
import { DeviceInfoPage } from './components/DeviceInfoPage';
import { KeymapPage } from './components/KeymapPage';
import { SettingsPage } from './components/SettingsPage';
import { TrayIconManager } from './TrayIconManager';


// Define BatteryStatus interface locally
interface BatteryStatus {
    percentage: number;
    status: number; // Raw battery status from device
}


// Define page types
type PageType = 'device-info' | 'settings' | 'keymap' | 'config' | 'about';

function App() {
    const [batteryStatus, setBatteryStatus] = useState<BatteryStatus | null>(null);
    const [magicStickDevices, setMagicStickDevices] = useState<main.MagicStickInfo[]>([]);
    const [selectedDevice, setSelectedDevice] = useState<main.MagicStickInfo | null>(null);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [isDeviceOpened, setIsDeviceOpened] = useState(false);
    const [isOpeningDevice, setIsOpeningDevice] = useState(false);
    const [isKeyboardConnected, setIsKeyboardConnected] = useState(false);
    const [currentDevice, setCurrentDevice] = useState<any>(null); // MagicStickDevice instance
    const [currentPage, setCurrentPage] = useState<PageType>('device-info');
    const [versionInfo, setVersionInfo] = useState<main.SemVerInfo | null>(null);

    // Reset to Device Info tab when selected device changes
    useEffect(() => {
        setCurrentPage('device-info');

        // Update tray icon based on device selection
        if (!selectedDevice) {
            // No device selected - show Missing icon
            trayIconManager.updateMissingIcon();
        }
    }, [selectedDevice?.Serial]);

    // Create tray icon manager instance
    const trayIconManager = new TrayIconManager();

    // Set up event listeners once on component mount
    useEffect(() => {
        // Set up battery event listener
        console.log('[Frontend] Setting up battery event listener...');
        EventsOn('battery-report', (data: any) => {
            console.log('[Frontend] Battery event received:', data);
            if (data && typeof data.status === 'number' && typeof data.level === 'number') {
                console.log('[Frontend] Battery data:', { status: data.status, level: data.level });
                setBatteryStatus({
                    percentage: data.level,
                    status: data.status
                });
                setIsKeyboardConnected(true);

                // Update tray icon with battery data using TrayIconManager
                trayIconManager.updateBatteryIcon(data.level, data.status);
            }
        });

        // Set up device disconnected event listener
        console.log('[Frontend] Setting up device-disconnected event listener...');
        EventsOn('device-disconnected', (data: any) => {
            console.log('[Frontend] Device disconnected event received:', data);
            setIsDeviceOpened(false);
            setIsKeyboardConnected(false);
            setCurrentDevice(null);
            setBatteryStatus(null);

            // Update tray icon to show device missing using TrayIconManager
            trayIconManager.updateMissingIcon();
        });

        // Set up RPC event listener
        console.log('[Frontend] Setting up rpc-event listener...');
        EventsOn('rpc-event', (data: any) => {
            console.log('[Frontend] RPC Event received:', data);
            if (data && data.name) {
                if (data.name === 'connected') {
                    setIsKeyboardConnected(true);
                } else if (data.name === 'disconnected') {
                    setIsKeyboardConnected(false);
                    setBatteryStatus(null);

                    // Update tray icon to show device missing
                    trayIconManager.updateMissingIcon();
                }
            }
        });

        // Cleanup function to remove event listeners
        return () => {
            console.log('[Frontend] Cleaning up event listeners...');
            // Note: Wails doesn't provide EventsOff, but listeners are automatically cleaned up
            // when the component unmounts or the app closes
        };
    }, []); // Empty dependency array - set up once on mount

    // Load devices on component mount
    useEffect(() => {
        // Load devices on startup
        refreshMagicStickDevices();

        // Set initial tray icon to Missing (no device selected)
        trayIconManager.updateMissingIcon();

        // Load version information
        const loadVersionInfo = async () => {
            try {
                const version = await AppBackend.GetSemVer();
                setVersionInfo(version);
            } catch (error) {
                console.error('Failed to load version info:', error);
            }
        };
        loadVersionInfo();
    }, []); // Empty dependency array for startup only

    // Auto-select last used device when devices are loaded
    useEffect(() => {
        if (magicStickDevices.length > 0 && !selectedDevice) {
            const lastSelectedSerial = localStorage.getItem('lastSelectedDevice');
            if (lastSelectedSerial) {
                const lastDevice = magicStickDevices.find(device => device.Serial === lastSelectedSerial);
                if (lastDevice) {
                    console.log('Auto-selecting and connecting to last used device:', lastDevice.Serial);
                    setSelectedDevice(lastDevice);
                    // Also attempt to connect to the device
                    openDevice(lastDevice.Serial);
                }
            }
        }
    }, [magicStickDevices, selectedDevice]);


    async function refreshMagicStickDevices() {
        setIsRefreshing(true);
        try {
            const devices = await MagicStickDevice.EnumerateDevices();
            setMagicStickDevices(devices);
            setIsRefreshing(false);
            return devices;
        } catch (error) {
            console.error('Failed to refresh MagicStick devices:', error);
            setIsRefreshing(false);
            throw error;
        }
    }

    function handleDeviceSelection(event: React.ChangeEvent<HTMLSelectElement>) {
        const deviceSerial = event.target.value;

        if (deviceSerial && deviceSerial !== "") {
            const device = magicStickDevices.find(d => d.Serial === deviceSerial);
            if (device) {
                setSelectedDevice(device);
                // Clear battery status when switching devices
                setBatteryStatus(null);
                setIsKeyboardConnected(false);

                // Save the selected device to localStorage for persistence
                localStorage.setItem('lastSelectedDevice', device.Serial);

                // Automatically open the device when selected
                openDevice(device.Serial);
            }
        } else {
            setSelectedDevice(null);
            // Clear battery status when no device is selected
            setBatteryStatus(null);
            setIsKeyboardConnected(false);
            // Clear the saved device when no device is selected
            localStorage.removeItem('lastSelectedDevice');
            // Close any opened device
            if (isDeviceOpened) {
                closeDevice();
            }
        }
    }

    async function openDevice(deviceSerial: string) {
        setIsOpeningDevice(true);

        // Find the device info
        const deviceInfo = magicStickDevices.find(d => d.Serial === deviceSerial);
        if (!deviceInfo) {
            console.error('Device not found:', deviceSerial);
            setIsOpeningDevice(false);
            return;
        }

        try {
            // Set up App bridge callbacks and establish connection to MagicStickDevice
            console.log('[Frontend] Setting up App bridge callbacks...');

            const batteryCallbackPromise = AppBackend.SetBatteryCallback().then(() => {
                console.log('[Frontend] Battery callback bridge setup completed');
            }).catch((error: any) => {
                console.error('[Frontend] Failed to set battery callback bridge:', error);
            });

            const disconnectCallbackPromise = AppBackend.SetDisconnectCallback().then(() => {
                console.log('[Frontend] Disconnect callback bridge setup completed');
            }).catch((error: any) => {
                console.error('[Frontend] Failed to set disconnect callback bridge:', error);
            });

            const rpcCallbackPromise = AppBackend.SetRpcEventCallback().then(() => {
                console.log('[Frontend] RPC callback bridge setup completed');
            }).catch((error: any) => {
                console.error('[Frontend] Failed to set RPC callback bridge:', error);
            });

            // Establish connection between App and MagicStickDevice
            const connectPromise = AppBackend.SetGlobalMagicStickDevice().then(() => {
                console.log('[Frontend] Global MagicStickDevice connection established');
            }).catch((error: any) => {
                console.error('[Frontend] Failed to establish MagicStickDevice connection:', error);
            });

            // Wait for callbacks to be set before opening device
            await Promise.all([batteryCallbackPromise, disconnectCallbackPromise, rpcCallbackPromise, connectPromise]);

            console.log('[Frontend] All callbacks set, opening device by serial:', deviceInfo.Serial);

            // Use the static function which operates on the global instance
            await MagicStickDevice.Open(deviceInfo.Serial, false);

            console.log('[Frontend] Device opened successfully');
            setIsDeviceOpened(true);
            setIsOpeningDevice(false);
            setCurrentDevice(deviceInfo); // Store device info

            // Update tray icon to show device connected using TrayIconManager
            trayIconManager.updateConnectedIcon();

            // Request initial battery report
            console.log('[Frontend] Requesting battery report...');
            await MagicStickDevice.RequestBatteryReport();

            console.log('[Frontend] Battery report requested successfully');

        } catch (error) {
            console.error('[Frontend] Failed to open device or set callbacks:', error);
            setIsOpeningDevice(false);
            setIsDeviceOpened(false);
            setCurrentDevice(null);
        }
    }

    async function closeDevice() {
        console.log('[Frontend] Closing device...');
        try {
            await MagicStickDevice.Close();
            console.log('[Frontend] Device closed successfully');
            setIsDeviceOpened(false);
            setIsKeyboardConnected(false);
            setCurrentDevice(null);
        } catch (error) {
            console.error('[Frontend] Failed to close device:', error);
        }
    }


    return (
        <div id="App" className="d-flex flex-column vh-100 bg-light">
            {/* Top Menu Bar - Fixed */}
            <nav className="navbar navbar-dark bg-dark shadow-sm" style={{ position: 'fixed', top: 0, left: 0, right: 0, zIndex: 1000 }}>
                <div className="container-fluid">
                    <div className="d-flex align-items-center">
                        <img
                            src={appicon}
                            alt="MagicStick Icon"
                            className="me-3"
                            style={{ width: '64px', height: '64px' }}
                        />
                        <h1 className="navbar-brand mb-0 text-white">magicstick</h1>
                    </div>
                    <div className="d-flex align-items-center gap-3">
                        <select
                            className="form-select"
                            style={{ minWidth: '300px' }}
                            value={selectedDevice ? selectedDevice.Serial : ""}
                            onChange={handleDeviceSelection}
                        >
                            <option value="">Select a device...</option>
                            {magicStickDevices.map((device) => (
                                <option key={device.Serial} value={device.Serial}>
                                    {device.Product} {device.Serial ? `(${device.Serial})` : ''}
                                </option>
                            ))}
                        </select>
                        <button
                            className="btn btn-primary"
                            onClick={refreshMagicStickDevices}
                            disabled={isRefreshing}
                        >
                            {isRefreshing ? 'Refreshing...' : 'Refresh'}
                        </button>
                    </div>
                </div>
            </nav>

            {/* Main Content Area - Below fixed navbar */}
            <div className="d-flex" style={{ marginTop: '80px', height: 'calc(100vh - 80px)' }}>
                {/* Left Navigation - Fixed height */}
                <div className="bg-dark d-flex flex-column" style={{ width: '200px', minWidth: '200px', height: '100%' }}>
                    <nav className="nav flex-column p-3 flex-grow-1">
                        <button
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'device-info' ? 'active' : ''} ${!selectedDevice ? 'disabled' : ''}`}
                            onClick={() => selectedDevice && setCurrentPage('device-info')}
                            disabled={!selectedDevice}
                        >
                            📱 Device Info
                        </button>
                        <button
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'settings' ? 'active' : ''} ${!selectedDevice ? 'disabled' : ''}`}
                            onClick={() => selectedDevice && setCurrentPage('settings')}
                            disabled={!selectedDevice}
                        >
                            ⚙️ Settings
                        </button>
                        <button
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'keymap' ? 'active' : ''} ${!selectedDevice ? 'disabled' : ''}`}
                            onClick={() => selectedDevice && setCurrentPage('keymap')}
                            disabled={!selectedDevice}
                        >
                            ⌨️ Keymap
                        </button>
                        <button
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'config' ? 'active' : ''} ${!selectedDevice ? 'disabled' : ''}`}
                            onClick={() => selectedDevice && setCurrentPage('config')}
                            disabled={!selectedDevice}
                        >
                            💾 Config
                        </button>
                        <button
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'about' ? 'active' : ''}`}
                            onClick={() => setCurrentPage('about')}
                        >
                            ℹ️ About
                        </button>
                    </nav>

                    {/* Version Information - Fixed at bottom of sidebar */}
                    {versionInfo && (
                        <div className="p-2">
                            <small className="text-secondary">v{versionInfo.version}</small>
                        </div>
                    )}
                </div>

                {/* Content Area - Scrollable */}
                <div className="flex-grow-1 overflow-auto bg-white" style={{ height: '100%' }}>
                    {currentPage === 'device-info' && (
                        <DeviceInfoPage
                            selectedDevice={selectedDevice}
                            isDeviceOpened={isDeviceOpened}
                            isOpeningDevice={isOpeningDevice}
                            isKeyboardConnected={isKeyboardConnected}
                            batteryStatus={batteryStatus}
                            onReconnect={() => selectedDevice && openDevice(selectedDevice.Serial)}
                        />
                    )}
                    {currentPage === 'settings' && (
                        <SettingsPage
                            selectedDevice={selectedDevice}
                            isDeviceOpened={isDeviceOpened}
                        />
                    )}
                    {currentPage === 'keymap' && (
                        <KeymapPage
                            selectedDevice={selectedDevice}
                            isDeviceOpened={isDeviceOpened}
                        />
                    )}
                    {currentPage === 'config' && (
                        <ConfigPage
                            selectedDevice={selectedDevice}
                            isDeviceOpened={isDeviceOpened}
                        />
                    )}
                    {currentPage === 'about' && (
                        <AboutPage />
                    )}
                </div>
            </div>
        </div>
    )
}

export default App
