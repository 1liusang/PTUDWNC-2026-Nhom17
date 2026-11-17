import NextAuth, { CredentialsSignin, type NextAuthConfig } from "next-auth";
import type { JWT } from "next-auth/jwt";
import Credentials from "next-auth/providers/credentials";
import { ApiError } from "@/lib/api/client";
import {
  clientHeaders,
  loginWithGoogle,
  loginWithPassword,
  refreshSession,
  revokeRefreshToken,
} from "./api/backend";
import { GOOGLE_PROVIDER_ID, LOGIN_PATH } from "./constants";
import { loginSchema } from "./schemas";
import type { AuthResponse } from "./types";

// Làm mới trước khi access token (15 phút) hết hạn 1 phút, để request đang chạy không bị 401.
const REFRESH_MARGIN_MS = 60_000;

const DEV_ONLY_SECRET = "culinary-blog-dev-only-secret-do-not-use-in-production";

/** Mang mã lỗi backend về form đăng nhập qua `signIn(...).code`. */
class BackendSignInError extends CredentialsSignin {
  constructor(code: string) {
    super();
    this.code = code;
  }
}

/**
 * OAuth Client ID của Google (công khai, commit trong .env.development). Để trống thì ẩn nút Google.
 * Không cần Client secret: Google Identity Services trả id_token thẳng cho trình duyệt (S-05).
 */
const googleClientId = process.env.AUTH_GOOGLE_ID?.trim() || null;

function toToken({ accessToken, accessTokenExpiresAt, refreshToken, user }: AuthResponse): JWT {
  return {
    accessToken,
    accessTokenExpiresAt: Date.parse(accessTokenExpiresAt),
    refreshToken,
    user: {
      id: user.id,
      email: user.email,
      userName: user.userName,
      fullName: user.fullName,
      avatarUrl: user.avatarUrl,
      roles: user.roles,
    },
  };
}

async function refreshIfNeeded(token: JWT): Promise<JWT> {
  const expiresAt = token.accessTokenExpiresAt ?? 0;
  if (token.error || Date.now() < expiresAt - REFRESH_MARGIN_MS) {
    return token;
  }

  if (!token.refreshToken) {
    return { ...token, error: "RefreshTokenError" };
  }

  try {
    return toToken(await refreshSession(token.refreshToken));
  } catch {
    return { ...token, error: "RefreshTokenError" };
  }
}

const config: NextAuthConfig = {
  secret: process.env.AUTH_SECRET ?? (process.env.NODE_ENV === "production" ? undefined : DEV_ONLY_SECRET),
  session: { strategy: "jwt" },
  pages: { signIn: LOGIN_PATH, error: LOGIN_PATH },
  logger: {
    // Sai mật khẩu là tình huống bình thường, không cần in stack trace ra log server.
    error(error) {
      if (error instanceof CredentialsSignin) {
        return;
      }
      console.error(error);
    },
  },
  providers: [
    Credentials({
      credentials: { email: {}, password: {} },
      async authorize(credentials, request) {
        const parsed = loginSchema.safeParse(credentials);
        if (!parsed.success) {
          throw new BackendSignInError("VALIDATION_ERROR");
        }

        try {
          const auth = await loginWithPassword(parsed.data, clientHeaders(request.headers));
          return {
            id: auth.user.id,
            email: auth.user.email,
            name: auth.user.fullName,
            image: auth.user.avatarUrl,
            backendAuth: auth,
          };
        } catch (error) {
          throw new BackendSignInError(error instanceof ApiError ? error.code : "SERVICE_UNAVAILABLE");
        }
      },
    }),
    // S-05: nhận id_token từ nút Google Identity Services; backend tự xác minh chữ ký, audience, email_verified.
    Credentials({
      id: GOOGLE_PROVIDER_ID,
      name: "Google",
      credentials: { idToken: {} },
      async authorize(credentials, request) {
        const idToken = typeof credentials?.idToken === "string" ? credentials.idToken : "";
        if (!googleClientId || !idToken) {
          throw new BackendSignInError(googleClientId ? "AUTH_GOOGLE_TOKEN_INVALID" : "AUTH_GOOGLE_UNAVAILABLE");
        }

        try {
          const auth = await loginWithGoogle(idToken, clientHeaders(request.headers));
          return {
            id: auth.user.id,
            email: auth.user.email,
            name: auth.user.fullName,
            image: auth.user.avatarUrl,
            backendAuth: auth,
          };
        } catch (error) {
          throw new BackendSignInError(error instanceof ApiError ? error.code : "SERVICE_UNAVAILABLE");
        }
      },
    }),
  ],
  callbacks: {
    async jwt({ token, user, trigger, session }) {
      if (user?.backendAuth) {
        return toToken(user.backendAuth);
      }

      if (trigger === "update" && token.user) {
        const fullName = typeof session?.fullName === "string" ? session.fullName : token.user.fullName;
        const email = typeof session?.email === "string" ? session.email : token.user.email;
        return { ...token, user: { ...token.user, fullName, email } };
      }

      return refreshIfNeeded(token);
    },
    session({ session, token }) {
      if (token.user) {
        session.user = {
          ...session.user,
          ...token.user,
          name: token.user.fullName,
          image: token.user.avatarUrl,
        };
      }
      session.accessToken = token.accessToken;
      session.error = token.error;
      return session;
    },
  },
  events: {
    // FR-AUTH-005: thu hồi refresh token ở backend; chạy phía server nên token không xuống trình duyệt.
    async signOut(message) {
      const token = "token" in message ? message.token : null;
      if (!token?.refreshToken) {
        return;
      }

      await revokeRefreshToken(token.refreshToken, token.error ? undefined : token.accessToken).catch(() => {
        // Thu hồi lỗi thì token vẫn tự hết hạn sau 7 ngày; không chặn việc đăng xuất.
      });
    },
  },
};

export const { handlers, auth, signIn, signOut } = NextAuth(config);

export { googleClientId };
