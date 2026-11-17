# Auth — API

- **Phụ trách:** TV1 — Nguyễn Ngọc Tuấn
- **FR:** FR-AUTH-001 → 007, FR-JOB-001

## Endpoint

| Method | Đường dẫn | Mô tả | Rate limit |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Đăng ký (Author), trả phiên đăng nhập — 201 | 5/phút/IP |
| POST | `/api/v1/auth/login` | Đăng nhập email + mật khẩu | 5/phút/IP |
| POST | `/api/v1/auth/google` | Đăng nhập bằng `idToken` của Google | 5/phút/IP |
| POST | `/api/v1/auth/refresh` | Xoay vòng refresh token | 30/phút/IP |
| POST | `/api/v1/auth/logout` | Thu hồi refresh token, luôn trả 204 | chung |
| GET | `/api/v1/auth/me` | Hồ sơ người đang đăng nhập | chung |
| PATCH | `/api/v1/auth/me` | Đổi `fullName` (2–100 ký tự) | chung |

Mọi request khác bị giới hạn 100/phút theo người dùng (hoặc IP nếu chưa đăng nhập); vượt hạn mức trả 429 kèm `Retry-After`. Mã lỗi: `docs/api/error-codes.md`.

## Dùng cho module khác

```csharp
using CulinaryBlog.API.Auth;

var recipes = api.MapGroup("/recipes").WithTags(Tag);
recipes.MapPost("/", CreateAsync).RequireAuthorization(AuthPolicies.Author); // Author hoặc Admin
categories.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(AuthPolicies.Admin);
```

Trong handler, lấy người đang gọi qua `ICurrentUser` (`CulinaryBlog.Application.Abstractions`): `UserId` là `Guid?`, null khi chưa đăng nhập.

## Lấy token để thử trên Scalar

Khi chạy ở Development, API tự migrate và tạo sẵn tài khoản mẫu:

| Email | Mật khẩu | Vai trò |
|---|---|---|
| `author1@culinaryblog.test` … `author3@culinaryblog.test` | `Author@12345` | Author |
| `admin@culinaryblog.test` | đặt qua user-secrets (xem dưới) | Admin, Author |

```bash
cd src/Backend
dotnet user-secrets set "Seed:Auth:AdminPassword" "<mật khẩu mạnh>" --project CulinaryBlog.API
```

Gọi `POST /api/v1/auth/login`, chép `accessToken` vào mục Authentication (Bearer) của Scalar. Token hết hạn sau 15 phút.

## Cấu hình

| Khóa | Mặc định | Ghi chú |
|---|---|---|
| `Jwt:SigningKey` | trống | ≥ 32 byte. Ở Development để trống thì API tự sinh khóa tạm (token mất hiệu lực khi khởi động lại). Production bắt buộc đặt qua biến môi trường `Jwt__SigningKey` |
| `Jwt:AccessTokenLifetimeMinutes` | 15 | |
| `Authentication:Google:ClientId` | trống | Để trống thì `/auth/google` trả 502. Cách tạo Client: `src/Frontend/features/auth/README.md` |
| `RateLimiting:*PermitLimit` | 5 / 30 / 100 | Xem `AuthRateLimitOptions` |
| `RateLimiting:TrustedProxyNetworks` | trống | CIDR của Nginx/mạng Docker để đọc `X-Forwarded-For` |
| `Smtp:Host`, `Smtp:Port` | `localhost`, `1025` | Mặc định gửi vào Mailpit (xem ở `http://localhost:8025`) |
| `Smtp:Username`, `Smtp:Password` | trống | Để trống thì không đăng nhập SMTP; port 465 bật SSL, cổng khác dùng STARTTLS nếu máy chủ hỗ trợ |
| `Smtp:SenderEmail`, `Smtp:SenderName` | `no-reply@culinaryblog.local` | Với Gmail phải trùng `Smtp:Username` |
| `Frontend:BaseUrl` | `http://localhost:3000` | Gốc của link trong email xác nhận / đặt lại mật khẩu |

## Gửi email thật (xác nhận email, quên mật khẩu, chào mừng)

Mặc định mọi email đi vào Mailpit, không tới hộp thư thật. Để gửi tới email cá nhân, trỏ `Smtp:*` sang một máy chủ SMTP thật bằng **user-secrets** (không ghi mật khẩu vào `appsettings*.json`):

```powershell
$p = "src/Backend/CulinaryBlog.API"
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com" --project $p
dotnet user-secrets set "Smtp:Port" "587" --project $p
dotnet user-secrets set "Smtp:Username" "<gmail gửi>@gmail.com" --project $p
dotnet user-secrets set "Smtp:Password" "<App Password 16 ký tự, bỏ dấu cách>" --project $p
dotnet user-secrets set "Smtp:SenderEmail" "<gmail gửi>@gmail.com" --project $p
```

- Gmail không nhận mật khẩu đăng nhập thường qua SMTP: bật **Xác minh 2 bước** cho tài khoản gửi rồi tạo mã ở https://myaccount.google.com/apppasswords. Giới hạn khoảng 500 email/ngày.
- Không muốn bật Xác minh 2 bước: dùng Brevo (`smtp-relay.brevo.com:587`, username là Login trên trang SMTP, password là SMTP key, sender phải được xác nhận trong Brevo; gói free 300 email/ngày). Email gửi qua Brevo với địa chỉ `@gmail.com` dễ vào Spam.
- Khởi động lại API sau khi đổi. Gửi lỗi thì xem tab **Failed** ở `/hangfire` (`535 Authentication failed` = sai App Password).
- Quay lại Mailpit: `"Host","Port","Username","Password","SenderEmail" | % { dotnet user-secrets remove "Smtp:$_" --project $p }`.
- Link trong email trỏ về `Frontend:BaseUrl` (mặc định `localhost:3000`) nên chỉ mở được trên máy đang chạy web; khi demo qua Cloudflare Tunnel đổi sang domain public.
