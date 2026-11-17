/** SessionProvider hỏi lại phiên định kỳ để token được làm mới và cookie được ghi lại trước khi access token (15 phút) hết hạn. */
export const SESSION_REFETCH_SECONDS = 4 * 60;

export const LOGIN_PATH = "/auth/login";
export const REGISTER_PATH = "/auth/register";
export const FORGOT_PASSWORD_PATH = "/auth/forgot-password";
export const RESET_PASSWORD_PATH = "/auth/reset-password";
export const CONFIRM_EMAIL_PATH = "/auth/confirm-email";
export const PROFILE_PATH = "/profile";
export const ADMIN_USERS_PATH = "/admin/users";

/** Giá trị `?error=` khi backend từ chối đăng nhập Google; `?code=` mang mã lỗi của backend. */
export const GOOGLE_SIGN_IN_ERROR = "GoogleSignIn";
