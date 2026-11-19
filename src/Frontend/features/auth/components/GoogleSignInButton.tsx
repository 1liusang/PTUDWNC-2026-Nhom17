"use client";

import { signIn } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { GOOGLE_PROVIDER_ID } from "../constants";
import { authErrorMessage, GOOGLE_SIGN_IN_FAILED_MESSAGE } from "../errors";
import { FormAlert } from "./FormAlert";

const GIS_SCRIPT_SRC = "https://accounts.google.com/gsi/client";
// Google giới hạn chiều rộng nút ở 400px.
const GIS_BUTTON_MAX_WIDTH = 400;
// Backend tự giới hạn 10 giây khi xác minh với Google; quá thời gian này coi như lỗi để không treo giao diện.
const SIGN_IN_TIMEOUT_MS = 20_000;

type GoogleCredentialResponse = { credential?: string };

type GoogleAccountsId = {
  initialize(options: {
    client_id: string;
    callback: (response: GoogleCredentialResponse) => void;
    ux_mode?: "popup" | "redirect";
    context?: "signin" | "signup" | "use";
    use_fedcm_for_button?: boolean;
  }): void;
  renderButton(
    parent: HTMLElement,
    options: { theme?: string; size?: string; text?: string; shape?: string; width?: number; locale?: string },
  ): void;
};

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } };
  }
}

let gisScript: Promise<void> | null = null;

function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T | "timeout"> {
  return Promise.race([promise, new Promise<"timeout">((resolve) => setTimeout(() => resolve("timeout"), ms))]);
}

/** Nạp thư viện Google Identity Services một lần cho cả trang. */
function loadGis(): Promise<void> {
  if (window.google?.accounts?.id) {
    return Promise.resolve();
  }

  gisScript ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = GIS_SCRIPT_SRC;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => {
      gisScript = null;
      reject(new Error("Could not load Google Identity Services."));
    };
    document.head.appendChild(script);
  });
  return gisScript;
}

type GoogleSignInButtonProps = {
  /** OAuth Client ID (công khai). Không cần Client secret: Google trả thẳng id_token cho trình duyệt (S-05). */
  clientId: string;
  callbackUrl: string;
  context?: "signin" | "signup";
};

export function GoogleSignInButton({ clientId, callbackUrl, context = "signin" }: GoogleSignInButtonProps) {
  const router = useRouter();
  const containerRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function handleCredential({ credential }: GoogleCredentialResponse) {
      if (!credential) {
        setError(GOOGLE_SIGN_IN_FAILED_MESSAGE);
        return;
      }

      setError(null);
      setPending(true);
      // Server Next.js gửi id_token sang POST /api/v1/auth/google để backend tự xác minh.
      const result = await withTimeout(
        signIn(GOOGLE_PROVIDER_ID, { idToken: credential, redirect: false }),
        SIGN_IN_TIMEOUT_MS,
      ).catch(() => undefined);
      if (result === "timeout") {
        setPending(false);
        setError(authErrorMessage("AUTH_GOOGLE_UNAVAILABLE"));
        return;
      }
      if (!result || result.error) {
        setPending(false);
        setError(authErrorMessage(result?.code));
        return;
      }

      router.replace(callbackUrl);
      router.refresh();
    }

    loadGis()
      .then(() => {
        const container = containerRef.current;
        const gis = window.google?.accounts.id;
        if (cancelled || !container || !gis) {
          return;
        }

        gis.initialize({
          client_id: clientId,
          callback: (response) => void handleCredential(response),
          ux_mode: "popup",
          context,
          use_fedcm_for_button: true,
        });
        container.replaceChildren();
        gis.renderButton(container, {
          theme: "filled_black",
          size: "large",
          text: context === "signup" ? "signup_with" : "continue_with",
          shape: "pill",
          width: Math.min(container.clientWidth || GIS_BUTTON_MAX_WIDTH, GIS_BUTTON_MAX_WIDTH),
          locale: "vi",
        });
      })
      .catch(() => {
        if (!cancelled) {
          setError("Không tải được nút đăng nhập Google. Kiểm tra kết nối mạng rồi tải lại trang.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, [clientId, callbackUrl, context, router]);

  // Nút hiển thị theo giao diện của trang; nút thật của Google nằm chồng lên trên, trong suốt,
  // để nhận cú bấm (Google không cho tự gọi popup đăng nhập từ nút riêng).
  return (
    <div className="flex flex-col gap-3">
      <div className={`group relative ${pending ? "pointer-events-none opacity-60" : ""}`} aria-busy={pending}>
        <div
          aria-hidden
          className="flex items-center justify-center gap-2 rounded-full border border-(color:--auth-input-border) bg-(color:--auth-surface) px-4 py-2.5 font-medium text-(color:--auth-fg) transition group-hover:bg-(color:--auth-hover)"
        >
          <GoogleLogo />
          {pending ? "Đang đăng nhập bằng Google…" : context === "signup" ? "Đăng ký với Google" : "Tiếp tục với Google"}
        </div>
        <div
          ref={containerRef}
          className="absolute inset-0 flex items-center justify-center overflow-hidden rounded-full opacity-[0.01]"
        />
      </div>
      <FormAlert message={error} tone="error" />
    </div>
  );
}

function GoogleLogo() {
  return (
    <svg aria-hidden viewBox="0 0 48 48" className="size-5">
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.2 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 13 4 4 13 4 24s9 20 20 20 20-9 20-20c0-1.3-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.2 0-9.6-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
    </svg>
  );
}
