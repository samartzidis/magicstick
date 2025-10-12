import { useEffect, useState } from 'react';
import * as AppBackend from "../../wailsjs/go/main/App";
import appicon from '../assets/images/appicon.png';

// About Page Component
export function AboutPage() {
    const [versionInfo, setVersionInfo] = useState<any>(null);

    useEffect(() => {
        const loadVersionInfo = async () => {
            try {
                const info = await AppBackend.GetSemVer();
                setVersionInfo(info);
            } catch (error) {
                console.error('Failed to load version info:', error);
            }
        };

        loadVersionInfo();
    }, []);

    return (
        <div className="container-fluid p-4">
            <h1 className="mb-4">About</h1>

            <div className="card mb-4">
                <div className="card-body">
                    <div className="d-flex align-items-center mb-3">
                        <img
                            src={appicon}
                            alt="MagicStick Icon"
                            className="me-3"
                            style={{ width: '80px', height: '80px' }}
                        />
                        <div>
                            <h2 className="mb-2">magicstick UI utility</h2>
                            {versionInfo && (
                                <p className="text-muted mb-0">Version {versionInfo.version}</p>
                            )}
                        </div>
                    </div>

                    <p className="mb-0">
                        magicstick UI utility is a comprehensive management tool for magicstick devices.
                        It provides device configuration, keymap editing, settings management, and real-time power monitoring.
                    </p>
                </div>
            </div>

            <div className="card">
                <div className="card-body">
                    <h5 className="card-title">Copyright</h5>
                    <p className="text-muted mb-0">
                        ©2025 magicstick.io. All rights reserved.<br />
                        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" className="me-1" style={{ verticalAlign: 'text-bottom' }}>
                            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
                        </svg>
                        https://github.com/samartzidis/magicstick
                    </p>
                </div>
            </div>
        </div>
    );
}
