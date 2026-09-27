import { useState, useEffect } from "react";
import { openUrl } from "@tauri-apps/plugin-opener";
import * as AppService from "../bindings/AppService";
import type { VersionInfo } from "../types";

const PROJECT_URL = "https://github.com/samartzidis/magicstick";

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-2">
      <span className="text-sm text-gray-500 dark:text-gray-400 whitespace-nowrap min-w-[130px] shrink-0">
        {label}
      </span>
      <span className="text-sm font-mono">{value}</span>
    </div>
  );
}

export function AboutPage() {
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);

  useEffect(() => {
    AppService.GetVersionInfo()
      .then(setVersionInfo)
      .catch(() => {});
  }, []);

  return (
    <div className="flex flex-col gap-4">

      {/* Version info */}
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <h3 className="text-base font-semibold m-0 mb-4">Version Information</h3>
        <div className="flex flex-col gap-2">
          <InfoRow
            label="App Version"
            value={versionInfo?.appVersion ?? "—"}
          />
          <InfoRow
            label=".NET Runtime"
            value={versionInfo?.dotNetVersion ?? "—"}
          />
        </div>
      </div>

      {/* Project link */}
      <div className="py-5 px-6 border border-gray-200 dark:border-[#3a3a3a] rounded-lg bg-black/[0.02] dark:bg-white/[0.03]">
        <h3 className="text-base font-semibold m-0 mb-2">Project Information</h3>
        <p className="text-sm text-gray-500 dark:text-gray-400 m-0 flex items-center gap-2">
          <svg
            width="16"
            height="16"
            viewBox="0 0 16 16"
            fill="currentColor"
            className="shrink-0"
          >
            <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" />
          </svg>
          <a
            href={PROJECT_URL}
            onClick={(e) => {
              e.preventDefault();
              openUrl(PROJECT_URL);
            }}
            className="text-inherit hover:underline"
          >
            github.com/samartzidis/magicstick
          </a>
        </p>
      </div>
    </div>
  );
}
