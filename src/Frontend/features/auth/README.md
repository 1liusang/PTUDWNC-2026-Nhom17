# features/auth

- **Phụ trách:** TV1
- **FR:** FR-AUTH-001 → 007
- **Route:** `/auth/login`, `/auth/register`, `/profile` (và bảo vệ `/dashboard/*`)

Auth.js v5 theo mô hình BFF (ADR S-05): phiên nằm trong cookie HttpOnly đã mã hóa, bên trong giữ access token (15 phút) và refresh token của backend. Token được làm mới trong callback `jwt` trước khi hết hạn 1 phút.

## Cấu trúc

| File | Nội dung |
|---|---|
| `auth.ts` | Cấu hình Auth.js: Credentials (mật khẩu) và `google-id-token` (id_token từ Google Identity Services), refresh token, thu hồi khi đăng xuất |
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

Xem `.env.example`: `AUTH_SECRET` (bắt buộc ở production), `AUTH_GOOGLE_ID` (Client ID Google, dev đã có trong `.env.development`).

## Đăng nhập Google (FR-AUTH-003)

**Không cần cấu hình gì khi dev:** pull về là có nút Google. Client ID (công khai, không phải secret) đã commit ở
`src/Frontend/.env.development` (`AUTH_GOOGLE_ID`) và `src/Backend/CulinaryBlog.API/appsettings.Development.json`
(`Authentication:Google:ClientId`). Không có Client secret nào trong hệ thống.

Luồng (S-05): `GoogleSignInButton` nạp Google Identity Services (`accounts.google.com/gsi/client`) và vẽ nút chính chủ của Google →
người dùng chọn tài khoản trong popup → Google trả `id_token` cho trình duyệt → `signIn("google-id-token", { idToken })`
→ provider Credentials trong `auth.ts` gọi `POST /api/v1/auth/google { idToken }` → backend tự xác minh chữ ký/audience/`email_verified`,
tìm theo Google `sub`, nếu chưa có thì liên kết với tài khoản cùng email hoặc tạo tài khoản Author mới (username sinh từ email, trùng thì
thêm hậu tố số; gửi email chào mừng). Backend từ chối thì nút hiện thông báo theo mã lỗi (`errors.ts`).

Google Cloud Console (project `culinaryblog-515009`, OAuth Client loại **Web application**):

- **Authorized JavaScript origins** phải có cả `http://localhost` và `http://localhost:3000` (thêm domain Tunnel khi demo). Thiếu origin thì nút báo lỗi `origin_mismatch`.
- Không cần Authorized redirect URI.
- App ở chế độ *Testing* chỉ cho các email trong **Audience → Test users** đăng nhập; muốn ai cũng dùng được thì **Publish app** (chỉ xin `openid email profile` nên không cần Google duyệt).

Dùng Client ID khác (ví dụ project Google riêng): đặt `AUTH_GOOGLE_ID` trong `.env.local` và
`dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/Backend/CulinaryBlog.API` — hai giá trị phải giống nhau.
Ẩn nút Google: đặt `AUTH_GOOGLE_ID=` (rỗng) trong `.env.local`.

`.env.development` chỉ được Next.js đọc khi `npm run dev` (và `.dockerignore` bỏ qua mọi `.env*`). Với Docker, `docker-compose.yml`
đã truyền `AUTH_GOOGLE_ID` vào container `web` (mặc định là Client ID trên; ghi đè bằng `AUTH_GOOGLE_ID` trong `.env` gốc);
container `api` chạy `ASPNETCORE_ENVIRONMENT=Development` nên đọc Client ID từ `appsettings.Development.json`.
Sau khi pull: `docker compose up -d --force-recreate web`.

Tài khoản tạo bằng Google không có mật khẩu; muốn đăng nhập bằng email/mật khẩu thì dùng "Quên mật khẩu" để đặt.
