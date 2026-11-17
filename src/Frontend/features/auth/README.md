# features/auth

- **Phụ trách:** TV1
- **FR:** FR-AUTH-001 → 007
- **Route:** `/auth/login`, `/auth/register`, `/profile` (và bảo vệ `/dashboard/*`)

Auth.js v5 theo mô hình BFF (ADR S-05): phiên nằm trong cookie HttpOnly đã mã hóa, bên trong giữ access token (15 phút) và refresh token của backend. Token được làm mới trong callback `jwt` trước khi hết hạn 1 phút.

## Cấu trúc

| File | Nội dung |
|---|---|
| `auth.ts` | Cấu hình Auth.js: Credentials, Google (khi có `AUTH_GOOGLE_ID`), refresh token, thu hồi khi đăng xuất |
| `server.ts` | `getServerAccessToken()`, `requireSession()` cho Server Component |
| `api/backend.ts` | Gọi `/api/v1/auth/*` từ phía server (trong Auth.js) |
| `api/client.ts` | Gọi từ trình duyệt: đăng ký, sửa hồ sơ |
| `components/` | Form đăng nhập/đăng ký/hồ sơ, `UserMenu`, `AuthSessionProvider` |
| `errors.ts` | Thông báo tiếng Việt theo mã lỗi backend |
| `/proxy.ts` (gốc frontend) | Chưa đăng nhập mà vào `/dashboard/*`, `/profile` → chuyển về `/auth/login?callbackUrl=...` |

## Dùng ở module khác

```tsx
// Client Component: apiClient tự gắn token của phiên hiện tại.
const recipes = await apiClient.get<PagedResult<Recipe>>("/api/v1/me/recipes");

// Server Component: lấy token rồi truyền vào.
import { requireSession } from "@/features/auth/server";
const session = await requireSession("/dashboard/recipes");
const recipes = await apiClient.get("/api/v1/me/recipes", { accessToken: session.accessToken });

// Thông tin người dùng: useSession() (client) hoặc auth() (server).
session.user.id; session.user.fullName; session.user.roles; // ["Author"] hoặc ["Admin", "Author"]
```

Tài khoản mẫu khi chạy dev: `author1@culinaryblog.test` / `Author@12345` (xem `src/Backend/CulinaryBlog.API/Endpoints/Auth/README.md`).

## Biến môi trường

Xem `.env.example`: `AUTH_SECRET` (bắt buộc ở production), `AUTH_GOOGLE_ID`, `AUTH_GOOGLE_SECRET`.

## Bật đăng nhập Google (FR-AUTH-003)

Nút "Tiếp tục với Google" chỉ hiện khi đã đặt **cả** `AUTH_GOOGLE_ID` và `AUTH_GOOGLE_SECRET`.

1. Google Cloud Console → *APIs & Services* → *OAuth consent screen*: chọn **External**, thêm email của nhóm vào *Test users* (khi app còn ở chế độ Testing chỉ test user đăng nhập được).
2. *Credentials* → *Create credentials* → *OAuth client ID* → **Web application**:
   - Authorized JavaScript origins: `http://localhost:3000` (và domain Tunnel nếu demo).
   - Authorized redirect URIs: `http://localhost:3000/api/auth/callback/google` (và `https://<domain>/api/auth/callback/google`).
3. Frontend — `src/Frontend/.env.local` (không commit):
   ```
   AUTH_GOOGLE_ID=<client-id>.apps.googleusercontent.com
   AUTH_GOOGLE_SECRET=<client-secret>
   ```
4. Backend — **cùng Client ID** (không cần secret):
   ```powershell
   dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>.apps.googleusercontent.com" --project src/Backend/CulinaryBlog.API
   ```
   Thiếu Client ID ở backend → đăng nhập Google trả `AUTH_GOOGLE_UNAVAILABLE`.
5. Khởi động lại `npm run dev` và API.

Luồng (S-05): Auth.js lấy `id_token` của Google → callback `signIn` gọi `POST /api/v1/auth/google { idToken }` → backend tự xác minh chữ ký/audience/`email_verified`, tìm theo Google `sub`, nếu chưa có thì liên kết với tài khoản cùng email hoặc tạo tài khoản Author mới (username sinh từ email, trùng thì thêm hậu tố số; gửi email chào mừng). Backend từ chối → quay về `/auth/login?error=GoogleSignIn&code=<mã lỗi>` và hiện thông báo tương ứng.

Tài khoản tạo bằng Google không có mật khẩu; muốn đăng nhập bằng email/mật khẩu thì dùng "Quên mật khẩu" để đặt.
