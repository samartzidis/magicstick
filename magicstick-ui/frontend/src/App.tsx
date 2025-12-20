import { useEffect, useState, useRef } from 'react';
import { Events } from "@wailsio/runtime";
import * as Log from "../bindings/github.com/wailsapp/wails/v3/pkg/services/log/logservice";
import * as DeviceService from "../bindings/magicstick-ui/deviceservice";
import * as AppService from "../bindings/magicstick-ui/appservice";
import { BatteryReportEvent } from "../bindings/magicstick-ui/models";
import { EventNames } from './eventNames';
import './App.css';
import appicon from './assets/images/appicon.png';
import { AboutPage } from './components/AboutPage';
import { DeviceInfoPage } from './components/DeviceInfoPage';
import { KeymapPage } from './components/KeymapPage';
import { SettingsPage } from './components/SettingsPage';
import { TrayIconManager } from './TrayIconManager';

// Define page types
type PageType = 'device-info' | 'settings' | 'keymap' | 'about';

// DeviceInfo type - matches the structure from hid.DeviceInfo
interface DeviceInfo {
    Index: number;
    VendorID: number;
    ProductID: number;
    Product: string;
    Serial: string;
    Manufacturer: string;
}

function App() {
    const [batteryStatus, setBatteryStatus] = useState<BatteryReportEvent | null>(null);
    const [magicStickDevices, setMagicStickDevices] = useState<DeviceInfo[]>([]);
    const [selectedDevice, setSelectedDevice] = useState<DeviceInfo | null>(null);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [isDeviceOpened, setIsDeviceOpened] = useState(false);
    const [isOpeningDevice, setIsOpeningDevice] = useState(false);
    const [currentDevice, setCurrentDevice] = useState<any>(null); // MagicStickDevice instance
    const [currentPage, setCurrentPage] = useState<PageType>('device-info');
    const [version, setVersion] = useState<string | null>(null);
    const [error, setError] = useState<{ title: string, message: string } | null>(null);
    
    // Reconnection interval ref
    const reconnectIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const reconnectDeviceSerialRef = useRef<string | null>(null);
    
    // Refs to access current state values in event listeners
    const selectedDeviceRef = useRef<DeviceInfo | null>(null);
    const isDeviceOpenedRef = useRef<boolean>(false);
    const isOpeningDeviceRef = useRef<boolean>(false);
    
    // Tray icon manager instance
    const trayIconManagerRef = useRef<TrayIconManager | null>(null);
    
    // Battery poll interval ref
    const batteryPollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
    const batteryStatusRef = useRef<BatteryReportEvent | null>(null);
    
    // Keep refs in sync with state
    useEffect(() => {
        selectedDeviceRef.current = selectedDevice;
    }, [selectedDevice]);
    
    useEffect(() => {
        isDeviceOpenedRef.current = isDeviceOpened;
    }, [isDeviceOpened]);
    
    useEffect(() => {
        isOpeningDeviceRef.current = isOpeningDevice;
    }, [isOpeningDevice]);
    
    useEffect(() => {
        batteryStatusRef.current = batteryStatus;
    }, [batteryStatus]);
    
    // Battery polling - request battery report every 3s if no battery status, every 60s if we have it
    useEffect(() => {
        // Clear any existing interval
        if (batteryPollIntervalRef.current) {
            clearInterval(batteryPollIntervalRef.current);
            batteryPollIntervalRef.current = null;
        }
        
        // Start polling if device is opened
        if (isDeviceOpened) {
            // Poll every 3s if no battery status, every 180s if we have it
            const pollInterval = batteryStatus ? 180000 : 3000;
            Log.Debug(`[Frontend] Starting battery polling (interval: ${pollInterval}ms)...`);
            
            batteryPollIntervalRef.current = setInterval(async () => {
                try {
                    Log.Debug('[Frontend] Polling for battery report...');
                    await DeviceService.RequestBatteryReport();
                } catch (error) {
                    Log.Error('[Frontend] Failed to request battery report:', error);
                }
            }, pollInterval);
        }
        
        return () => {
            if (batteryPollIntervalRef.current) {
                clearInterval(batteryPollIntervalRef.current);
                batteryPollIntervalRef.current = null;
            }
        };
    }, [isDeviceOpened, batteryStatus]);
    
    // Handle selected device changes
    useEffect(() => {
        setCurrentPage('device-info');
        
        // Update tray icon based on device selection
        if (trayIconManagerRef.current && !selectedDevice) {
            trayIconManagerRef.current.updateMissingIcon();
        }
        
        // Stop any ongoing reconnection attempts
        if (reconnectIntervalRef.current) {
            clearInterval(reconnectIntervalRef.current);
            reconnectIntervalRef.current = null;
            reconnectDeviceSerialRef.current = null;
            Log.Debug('[Frontend] Stopping reconnection attempts due to device change');
        }
    }, [selectedDevice?.Serial]);
    
    // Stop reconnection attempts when device successfully opens
    useEffect(() => {
        if (isDeviceOpened && reconnectIntervalRef.current) {
            clearInterval(reconnectIntervalRef.current);
            reconnectIntervalRef.current = null;
            reconnectDeviceSerialRef.current = null;
            Log.Debug('[Frontend] Stopping reconnection attempts - device reconnected');
        }
    }, [isDeviceOpened]); // update when device is opened

    // Initialize app on mount
    useEffect(() => {
        // Initialize tray icon manager
        trayIconManagerRef.current = new TrayIconManager();
        trayIconManagerRef.current.updateMissingIcon();
        
        // Load devices and version info
        refreshMagicStickDevices();
        AppService.GetVersion().then(setVersion).catch((error) => {
            Log.Error('Failed to load version info:', error);
        });
        
        // Set up battery event listener
        Log.Debug('[Frontend] Setting up battery event listener...');
        const unsubscribeBattery = Events.On(EventNames.BatteryReport, (ev) => {
            Log.Debug('[Frontend] Battery event received:', ev.data);
            setBatteryStatus(ev.data);
            
            // Update tray icon with battery data using TrayIconManager
            if (trayIconManagerRef.current) {
                trayIconManagerRef.current.updateBatteryIcon(ev.data.level, ev.data.status);
            }
        });

        // Set up device disconnected event listener - ev.data is typed as DeviceDisconnectedEvent
        Log.Debug('[Frontend] Setting up device-disconnected event listener...');
        const unsubscribeDisconnected = Events.On(EventNames.DeviceDisconnected, (ev) => {
            Log.Debug('[Frontend] Device disconnected event received:', ev.data);
            setIsDeviceOpened(false);
            setCurrentDevice(null);
            setBatteryStatus(null);
            
            // Update tray icon to show device missing using TrayIconManager
            if (trayIconManagerRef.current) {
                trayIconManagerRef.current.updateMissingIcon();
            }
            
            // Start automatic reconnection if we have a selected device
            // Use ref to get current value
            const currentSelectedDevice = selectedDeviceRef.current;
            if (currentSelectedDevice && currentSelectedDevice.Serial) {
                Log.Debug('[Frontend] Starting automatic reconnection attempts for device:', currentSelectedDevice.Serial);
                reconnectDeviceSerialRef.current = currentSelectedDevice.Serial;
                
                // Clear any existing interval
                if (reconnectIntervalRef.current) {
                    clearInterval(reconnectIntervalRef.current);
                }
                
                // Start reconnection interval (every 3 seconds)
                reconnectIntervalRef.current = setInterval(async () => {
                    const currentSelected = selectedDeviceRef.current;
                    
                    // Stop if device changed, already connected, or currently opening
                    if (!currentSelected || currentSelected.Serial !== reconnectDeviceSerialRef.current || 
                        isDeviceOpenedRef.current || isOpeningDeviceRef.current) {
                        if (reconnectIntervalRef.current) {
                            clearInterval(reconnectIntervalRef.current);
                            reconnectIntervalRef.current = null;
                            reconnectDeviceSerialRef.current = null;
                        }
                        return;
                    }
                    
                    // Try to reconnect directly - let backend handle if device not available
                    try {
                        Log.Debug('[Frontend] Attempting to reconnect...');
                        await DeviceService.Open(currentSelected.Serial, false);
                        // Success - update state
                        setIsDeviceOpened(true);
                        setCurrentDevice(currentSelected);
                        if (trayIconManagerRef.current) {
                            trayIconManagerRef.current.updateConnectedIcon();
                        }

                        await DeviceService.RequestBatteryReport();

                    } catch {
                        // Device not ready yet, will retry
                    }
                }, 3000);
            }
        });

        // Set up RPC event listener - ev.data is typed as RpcEvent
        Log.Debug('[Frontend] Setting up rpc-event listener...');
        const unsubscribeRpc = Events.On(EventNames.Rpc, (ev) => {
            Log.Debug('[Frontend] RPC Event received:', ev.data);
            if (ev.data.name === 'disconnected') {
                setBatteryStatus(null);
                
                // Update tray icon to show device missing
                if (trayIconManagerRef.current) {
                    trayIconManagerRef.current.updateMissingIcon();
                }
            }
        });

        // Cleanup function to remove event listeners
        return () => {
            Log.Debug('[Frontend] Cleaning up event listeners...');
            unsubscribeBattery();
            unsubscribeDisconnected();
            unsubscribeRpc();
            
            if (reconnectIntervalRef.current) {
                clearInterval(reconnectIntervalRef.current);
                reconnectIntervalRef.current = null;
            }
        };
    }, []);

    // Auto-select last used device when devices are loaded
    useEffect(() => {
        if (magicStickDevices.length > 0 && !selectedDevice) {
            const lastSelectedSerial = localStorage.getItem('lastSelectedDevice');
            if (lastSelectedSerial) {
                const lastDevice = magicStickDevices.find(device => device.Serial === lastSelectedSerial);
                if (lastDevice) {
                    Log.Info('Auto-selecting and connecting to last used device:', lastDevice.Serial);
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
            const devices = await DeviceService.EnumerateDevices();
            // Cast to DeviceInfo[] since bindings return any[]
            setMagicStickDevices(devices as DeviceInfo[]);
            setIsRefreshing(false);
            return devices;
        } catch (error) {
            Log.Error('Failed to refresh MagicStick devices:', error);
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

                // Save the selected device to localStorage for persistence
                localStorage.setItem('lastSelectedDevice', device.Serial);

                // Automatically open the device when selected
                openDevice(device.Serial);
            }
        } else {
            setSelectedDevice(null);
            // Clear battery status when no device is selected
            setBatteryStatus(null);
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

        const deviceInfo = magicStickDevices.find(d => d.Serial === deviceSerial);
        if (!deviceInfo) {
            Log.Error('Device not found:', deviceSerial);
            setIsOpeningDevice(false);
            return;
        }

        try {
            Log.Debug('[Frontend] Opening device by serial:', deviceInfo.Serial);

            // Open device - callbacks are automatically set up in DeviceService.Open()
            await DeviceService.Open(deviceInfo.Serial, false);

            Log.Info('[Frontend] Device opened successfully');
            setIsDeviceOpened(true);
            setIsOpeningDevice(false);
            setCurrentDevice(deviceInfo); // Store device info
            setError(null); // Clear any previous errors

            // Update tray icon to show device connected using TrayIconManager
            if (trayIconManagerRef.current) {
                trayIconManagerRef.current.updateConnectedIcon();
            }

            // Request initial battery report
            await DeviceService.RequestBatteryReport();

        } catch (error) {
            Log.Error('[Frontend] Failed to open device or set callbacks:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError({
                title: "⚠️ Device Connection Error",
                message: `Failed to open device: ${errorMessage}`
            });
            setIsOpeningDevice(false);
            setIsDeviceOpened(false);
            setCurrentDevice(null);
        }
    }

    async function closeDevice() {
        Log.Debug('[Frontend] Closing device...');
        try {
            await DeviceService.Close();
            Log.Info('[Frontend] Device closed successfully');
            setIsDeviceOpened(false);
            setCurrentDevice(null);
        } catch (error) {
            Log.Error('[Frontend] Failed to close device:', error);
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
                            style={{ minWidth: '200px', maxWidth: '450px' }}
                            value={selectedDevice ? selectedDevice.Serial : ""}
                            onChange={handleDeviceSelection}
                        >
                            <option value="">Select a device...</option>
                            {magicStickDevices.map((device) => (
                                <option key={device.Serial} value={device.Serial}>
                                    {device.Product}
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

            {/* Error Modal */}
            {error && (
                <div className="modal fade show" style={{ display: 'block', backgroundColor: 'rgba(0,0,0,0.5)' }} tabIndex={-1}>
                    <div className="modal-dialog modal-dialog-centered">
                        <div className="modal-content">
                            <div className="modal-header">
                                <h5 className="modal-title text-danger">
                                    {error.title}
                                </h5>
                                <button type="button" className="btn-close" onClick={() => setError(null)} aria-label="Close"></button>
                            </div>
                            <div className="modal-body">
                                <p className="mb-0">{error.message}</p>
                            </div>
                            <div className="modal-footer">
                                <button type="button" className="btn btn-primary" onClick={() => setError(null)}>
                                    Close
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

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
                            className={`btn btn-outline-light mb-2 text-start ${currentPage === 'about' ? 'active' : ''}`}
                            onClick={() => setCurrentPage('about')}
                        >
                            ℹ️ About
                        </button>
                    </nav>

                    {/* Version Information - Fixed at bottom of sidebar */}
                    {version && (
                        <div className="p-2">
                            <small className="text-secondary">v{version}</small>
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
                            batteryStatus={batteryStatus}
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
                    {currentPage === 'about' && (
                        <AboutPage />
                    )}
                </div>
            </div>
        </div>
    )
}

export default App
