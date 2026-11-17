"use client";

import { signIn } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { GOOGLE_PROVIDER_ID } from "../constants";
import { authErrorMessage, GOOGLE_SIGN_IN_FAILED_MESSAGE } from "../errors";
import { FormAlert } from "./FormAlert";

const GIS_SCRIPT_SRC = "https://accounts.google.com/gsi/client";
const BUTTON_MAX_WIDTH = 400;

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
      const result = await signIn(GOOGLE_PROVIDER_ID, { idToken: credential, redirect: false });
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
          width: Math.min(container.clientWidth || BUTTON_MAX_WIDTH, BUTTON_MAX_WIDTH),
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

  return (
    <div className="flex flex-col gap-3">
      <div
        ref={containerRef}
        aria-busy={pending}
        className={`flex min-h-11 w-full justify-center ${pending ? "pointer-events-none opacity-60" : ""}`}
      />
      {pending && <p className="text-center text-sm text-(color:--auth-muted)">Đang đăng nhập bằng Google…</p>}
      <FormAlert message={error} tone="error" />
    </div>
  );
}
