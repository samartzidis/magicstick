import { redo, selectAll, undo } from "@codemirror/commands";
import CodeMirror, { type ReactCodeMirrorRef } from "@uiw/react-codemirror";
import { useCallback, useEffect, useRef, useState } from "react";
import * as DeviceManagerService from "../bindings/DeviceManagerService";
import type { GetKeymapResult } from "../bindings/models";
import { magicstick } from "../lib/magicstickLang";

interface KeymapPageProps {
  isDeviceOpened: boolean;
}

function usePrefersDark(): boolean {
  const [dark, setDark] = useState(() =>
    typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
  );
  useEffect(() => {
    const m = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => setDark(m.matches);
    m.addEventListener("change", handler);
    return () => m.removeEventListener("change", handler);
  }, []);
  return dark;
}

export function KeymapPage({ isDeviceOpened }: KeymapPageProps) {
  const prefersDark = usePrefersDark();
  const [keymap, setKeymap] = useState<GetKeymapResult | null>(null);
  const [editorContent, setEditorContent] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const isLoadingRef = useRef(false);
  const editorRef = useRef<ReactCodeMirrorRef>(null);
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number } | null>(null);
  const contextMenuRef = useRef<HTMLDivElement>(null);

  const closeContextMenu = useCallback(() => setContextMenu(null), []);

  useEffect(() => {
    if (!contextMenu) return;
    const onDocClick = (e: MouseEvent) => {
      if (contextMenuRef.current?.contains(e.target as Node)) return;
      closeContextMenu();
    };
    const onDocScroll = () => closeContextMenu();
    document.addEventListener("click", onDocClick, true);
    document.addEventListener("scroll", onDocScroll, true);
    return () => {
      document.removeEventListener("click", onDocClick, true);
      document.removeEventListener("scroll", onDocScroll, true);
    };
  }, [contextMenu, closeContextMenu]);

  const runEditorAction = useCallback(
    (action: "copy" | "cut" | "paste" | "selectAll") => {
      const view = editorRef.current?.view;
      if (!view) return;
      const state = view.state;
      const selection = state.selection.main;

      if (action === "selectAll") {
        selectAll(view);
        closeContextMenu();
        return;
      }

      if (action === "copy" || action === "cut") {
        const text =
          selection.from !== selection.to
            ? state.sliceDoc(selection.from, selection.to)
            : state.doc.toString();
        void navigator.clipboard.writeText(text).then(() => {
          if (action === "cut" && !state.readOnly && selection.from !== selection.to) {
            view.dispatch({
              changes: { from: selection.from, to: selection.to, insert: "" },
              userEvent: "delete.cut",
            });
          }
          closeContextMenu();
        });
        return;
      }

      if (action === "paste") {
        void navigator.clipboard.readText().then((text) => {
          if (!state.readOnly && text) {
            view.dispatch({
              changes: { from: selection.from, to: selection.to, insert: text },
              userEvent: "input.paste",
            });
          }
          closeContextMenu();
        });
      }
    },
    [closeContextMenu]
  );

  const loadKeymap = async (defaults = false) => {
    if (!isDeviceOpened) return;
    if (isLoadingRef.current) return;
    isLoadingRef.current = true;
    setIsLoading(true);
    setError(null);
    setInfo(null);
    try {
      await new Promise((r) => setTimeout(r, 500));
      const result = await DeviceManagerService.GetKeymap(defaults);
      if (result?.items) {
        setKeymap(result);
        setEditorContent(result.items.join("\n"));
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      if (msg.includes("timeout")) {
        setError(
          "Failed to load keymap: RPC timeout. The device may need more time. Try Reload again."
        );
      } else {
        setError(`Failed to load keymap: ${msg}`);
      }
    } finally {
      isLoadingRef.current = false;
      setIsLoading(false);
    }
  };

  const saveKeymap = async () => {
    if (!isDeviceOpened) return;
    setIsSaving(true);
    setError(null);
    setInfo(null);
    try {
      const lines = editorContent.split("\n").filter((line) => line.trim() !== "");
      const result = await DeviceManagerService.SetKeymap(lines);
      if (result.success) {
        setInfo("Keymap saved successfully.");
        if (keymap) setKeymap({ ...keymap, items: lines });
      } else {
        setError(result.error ? `Failed to apply keymap: ${result.error}` : "Failed to apply keymap.");
      }
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Failed to apply keymap: ${msg}`);
    } finally {
      setIsSaving(false);
    }
  };

  useEffect(() => {
    if (isDeviceOpened && !keymap && !isLoadingRef.current) {
      loadKeymap(false);
    }
  }, [isDeviceOpened]);

  if (!isDeviceOpened) {
    return (
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <h2 className="text-lg font-medium text-gray-500 dark:text-gray-400 m-0 mb-2">
          Device not connected
        </h2>
        <p className="text-gray-500 dark:text-gray-400 m-0">
          Connect to the device first to access its keymap.
        </p>
      </div>
    );
  }

  return (
    <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03] flex flex-col flex-1 min-h-0">

      {error && (
        <div
          className="mb-3 px-4 py-2 rounded-md bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-400"
          role="alert"
        >
          {error}
        </div>
      )}
      {info && (
        <div
          className="mb-3 px-4 py-2 rounded-md bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300"
          role="alert"
        >
          {info}
        </div>
      )}

      {isLoading ? (
        <p className="text-gray-500 dark:text-gray-400 py-4 m-0">Loading…</p>
      ) : keymap ? (
        <div className="flex flex-col gap-3 flex-1 min-h-0">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <p className="font-medium m-0 mb-0.5">
                {keymap.items.length} entries.
              </p>
              <p className="text-sm text-gray-500 dark:text-gray-400 m-0">
                One magicstick keymap command per line.
              </p>
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => {
                  const view = editorRef.current?.view;
                  if (view) undo(view);
                }}
                className="px-3 py-1.5 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700"
                title="Undo (Ctrl+Z)"
              >
                Undo
              </button>
              <button
                type="button"
                onClick={() => {
                  const view = editorRef.current?.view;
                  if (view) redo(view);
                }}
                className="px-3 py-1.5 text-sm rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700"
                title="Redo (Ctrl+Y)"
              >
                Redo
              </button>
            </div>
          </div>

          <div
            className="keymap-editor border border-gray-200 dark:border-[#3a3a3a] rounded-md overflow-auto flex-1 min-h-[200px] relative"
            onContextMenu={(e) => {
              e.preventDefault();
              setContextMenu({ x: e.clientX, y: e.clientY });
            }}
          >
            <CodeMirror
              ref={editorRef}
              value={editorContent}
              onChange={setEditorContent}
              theme={prefersDark ? "dark" : "light"}
              extensions={[magicstick()]}
              basicSetup={{ lineNumbers: true }}
              placeholder="Enter key mappings, one per line…"
              height="100%"
            />
            {contextMenu && (
              <div
                ref={contextMenuRef}
                className="fixed z-50 min-w-[140px] py-1 rounded-md border border-gray-200 dark:border-[#3a3a3a] bg-white dark:bg-gray-800 shadow-lg"
                style={{ left: contextMenu.x, top: contextMenu.y }}
                role="menu"
              >
                <button
                  type="button"
                  role="menuitem"
                  className="w-full text-left px-3 py-1.5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700"
                  onClick={() => runEditorAction("cut")}
                >
                  Cut
                </button>
                <button
                  type="button"
                  role="menuitem"
                  className="w-full text-left px-3 py-1.5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700"
                  onClick={() => runEditorAction("copy")}
                >
                  Copy
                </button>
                <button
                  type="button"
                  role="menuitem"
                  className="w-full text-left px-3 py-1.5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700"
                  onClick={() => runEditorAction("paste")}
                >
                  Paste
                </button>
                <button
                  type="button"
                  role="menuitem"
                  className="w-full text-left px-3 py-1.5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 border-t border-gray-100 dark:border-gray-700"
                  onClick={() => runEditorAction("selectAll")}
                >
                  Select all
                </button>
              </div>
            )}
          </div>

          <div className="flex flex-wrap gap-2 shrink-0">
            <button
              type="button"
              onClick={saveKeymap}
              disabled={isSaving}
              className="px-4 py-2 rounded-md bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white font-medium"
            >
              {isSaving ? "Applying…" : "Apply"}
            </button>
            <button
              type="button"
              onClick={() => loadKeymap(false)}
              disabled={isLoading}
              className="px-4 py-2 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50"
            >
              Reload
            </button>
            <button
              type="button"
              onClick={() => loadKeymap(true)}
              disabled={isLoading}
              className="px-4 py-2 rounded-md border border-indigo-500 text-indigo-600 dark:text-indigo-400 hover:bg-indigo-50 dark:hover:bg-indigo-900/20 disabled:opacity-50"
            >
              Load defaults
            </button>
          </div>
        </div>
      ) : (
        <div className="py-4 text-center">
          <p className="text-gray-500 dark:text-gray-400 mb-3 m-0">
            No keymap data. Click Reload to load the current keymap.
          </p>
          <button
            type="button"
            onClick={() => loadKeymap(false)}
            disabled={isLoading}
            className="px-4 py-2 rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50"
          >
            Reload keymap
          </button>
        </div>
      )}
    </div>
  );
}
