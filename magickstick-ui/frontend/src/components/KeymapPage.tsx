import { redo, undo } from '@codemirror/commands';
import CodeMirror from '@uiw/react-codemirror';
import { useEffect, useRef, useState } from 'react';
import * as Device from "../../wailsjs/go/hid/Device";
import { hid } from "../../wailsjs/go/models";
import { magicstick } from '../magicstickLang';

// Keymap Page Component
interface KeymapPageProps {
    selectedDevice: hid.DeviceInfo | null;
    isDeviceOpened: boolean;
}

export function KeymapPage({ selectedDevice, isDeviceOpened }: KeymapPageProps) {
    const [keymap, setKeymap] = useState<hid.GetKeymapReply | null>(null);
    const [editorContent, setEditorContent] = useState<string>('');
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [info, setInfo] = useState<string | null>(null);
    const [showDefaults, setShowDefaults] = useState(false);
    const isLoadingRef = useRef(false);
    const editorRef = useRef<any>(null);

    const loadKeymap = async (defaults: boolean = false) => {
        if (!selectedDevice || !isDeviceOpened) return;

        // Immediate synchronous check to prevent duplicate calls
        if (isLoadingRef.current) {
            console.log('[KeymapPage] loadKeymap already in progress, skipping');
            return;
        }

        console.log('[KeymapPage] loadKeymap called, defaults:', defaults);
        isLoadingRef.current = true;
        setIsLoading(true);
        setError(null);
        setInfo(null);
        try {
            console.log('Loading keymap for device:', selectedDevice.Serial, 'defaults:', defaults);

            // Add a small delay to ensure RPC connection is ready
            await new Promise(resolve => setTimeout(resolve, 1000));

            const deviceKeymap = await Device.GetKeymap(defaults);
            setKeymap(deviceKeymap);
            setEditorContent(deviceKeymap.items.join('\n'));
            setShowDefaults(defaults);
            setError(null);
            setInfo(null);
            console.log('Keymap loaded:', deviceKeymap);
        } catch (error) {
            console.error('Failed to load keymap:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);

            // Handle specific error types with user-friendly messages
            if (errorMessage.includes('RPC call already in progress')) {
                setInfo('Keymap is being loaded, please wait...');
            } else if (errorMessage.includes('timeout')) {
                setError('Failed to load keymap: RPC timeout. The device may need more time to initialize. Try clicking "Reload Keymap" again.');
            } else {
                setError(`Failed to load keymap: ${errorMessage}`);
            }
        } finally {
            isLoadingRef.current = false;
            setIsLoading(false);
        }
    };

    const saveKeymap = async () => {
        if (!selectedDevice || !isDeviceOpened) return;

        setIsSaving(true);
        setError(null);
        try {
            console.log('Saving keymap for device:', selectedDevice.Serial);

            // Convert editor content to keymap items
            const lines = editorContent.split('\n').filter(line => line.trim() !== '');
            const result = await Device.SetKeymap(lines);
            console.log('Keymap save result:', result);

            if (!result.success) {
                setError(`Failed to apply keymap: ${result.error}`);
            } else {
                setInfo('Keymap saved successfully!');
                // Update the keymap state with the saved content
                if (keymap) {
                    setKeymap({
                        ...keymap,
                        items: lines
                    });
                }
            }
        } catch (error) {
            console.error('Failed to apply keymap:', error);
            const errorMessage = error instanceof Error ? error.message : String(error);
            setError(`Failed to apply keymap: ${errorMessage}`);
        } finally {
            setIsSaving(false);
        }
    };

    const handleKeymapTextChange = (value: string) => {
        // Only update the editor content, don't update the keymap state
        // The keymap state will be updated when the user saves
        setEditorContent(value);
    };

    const getKeymapText = () => {
        return editorContent;
    };

    // Load keymap when Keymap tab becomes active, device is opened, and not already loading/loaded
    useEffect(() => {
        console.log('[KeymapPage] useEffect triggered - isDeviceOpened:', isDeviceOpened, 'selectedDevice:', selectedDevice?.Serial, 'isLoadingRef:', isLoadingRef.current, 'keymap:', !!keymap);
        if (isDeviceOpened && selectedDevice && !keymap && !isLoadingRef.current) {
            loadKeymap(false); // Load current keymap by default
        }
    }, [isDeviceOpened, selectedDevice?.Serial]);

    if (!selectedDevice) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">No Device Selected</h2>
                    <p className="text-muted">Please select a device from the dropdown above to manage its keymap.</p>
                </div>
            </div>
        );
    }

    if (!isDeviceOpened) {
        return (
            <div className="container-fluid p-4">
                <div className="text-center">
                    <h2 className="text-muted">Device Not Connected</h2>
                    <p className="text-muted">Please connect to the device first to access its keymap.</p>
                </div>
            </div>
        );
    }

    return (
        <div className="container-fluid p-4">
            <h1 className="mb-4">Keymap</h1>

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

            {isLoading ? (
                <div className="text-center p-4">
                    <p className="text-muted">Loading keymap...</p>
                </div>
            ) : keymap ? (
                <div className="card">
                    <div className="card-body">
                        <div className="mb-3">
                            <div className="d-flex justify-content-between align-items-center mb-2">
                                <div>
                                    <h5 className="mb-1">Keymap ({keymap.items.length} entries)</h5>
                                    <small className="text-muted">One magicstick keymap command per line.</small>
                                </div>
                                <div className="btn-group" role="group">
                                    <button
                                        className="btn btn-outline-secondary btn-sm"
                                        onClick={() => {
                                            if (editorRef.current?.view) {
                                                undo(editorRef.current.view);
                                            }
                                        }}
                                        title="Undo (Ctrl+Z)"
                                    >
                                        ↶ Undo
                                    </button>
                                    <button
                                        className="btn btn-outline-secondary btn-sm"
                                        onClick={() => {
                                            if (editorRef.current?.view) {
                                                redo(editorRef.current.view);
                                            }
                                        }}
                                        title="Redo (Ctrl+Y)"
                                    >
                                        ↷ Redo
                                    </button>
                                </div>
                            </div>
                        </div>

                        <div className="keymap-editor border rounded">
                            <CodeMirror
                                ref={editorRef}
                                value={getKeymapText()}
                                onChange={handleKeymapTextChange}
                                extensions={[magicstick()]}
                                basicSetup={{
                                    lineNumbers: true,
                                    foldGutter: true,
                                    dropCursor: false,
                                    allowMultipleSelections: false,
                                    indentOnInput: true,
                                    bracketMatching: true,
                                    closeBrackets: true,
                                    autocompletion: true,
                                    highlightActiveLine: false,
                                    highlightSelectionMatches: true,
                                    searchKeymap: true,
                                    historyKeymap: true,
                                    foldKeymap: true,
                                    completionKeymap: true,
                                    lintKeymap: true,
                                    defaultKeymap: true,
                                }}
                                placeholder="Enter key mappings, one per line..."
                            />
                        </div>

                        <div className="d-flex gap-2 mt-3">
                            <button
                                className="btn btn-success"
                                onClick={saveKeymap}
                                disabled={isSaving}
                            >
                                {isSaving ? 'Applying...' : 'Apply'}
                            </button>
                            <button
                                className="btn btn-secondary"
                                onClick={() => loadKeymap(false)}
                                disabled={isLoading}
                            >
                                Reload
                            </button>
                            <button
                                className="btn btn-outline-primary"
                                onClick={() => loadKeymap(true)}
                                disabled={isLoading}
                            >
                                Load Defaults
                            </button>
                        </div>
                    </div>
                </div>
            ) : (
                <div className="text-center p-4">
                    <p className="text-muted mb-3">No keymap data available. Click "Reload Keymap" to load the current keymap.</p>
                    <button
                        className="btn btn-secondary"
                        onClick={() => loadKeymap(false)}
                        disabled={isLoading}
                    >
                        Reload Keymap
                    </button>
                </div>
            )}
        </div>
    );
}
