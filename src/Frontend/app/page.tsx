// PLACEHOLDER: Trang chủ thật (ISR, revalidate 3600, danh sách công thức nổi bật)
// thuộc Module 4 (FR-SRCH/SEO) — Trần Lê Bảo Thư. File này chỉ để khung Frontend
// chạy được và có chỗ để test luồng đăng nhập/đăng ký; thay thế khi có code Module 4.
import Link from "next/link";

export default function HomePage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col items-center justify-center gap-4 p-8 text-center">
      <h1 className="text-3xl font-bold">Culinary Blog</h1>
      <p className="text-gray-600">
        Trang chủ chưa được implement (thuộc Module 4 — Search/SEO).
      </p>
      <div className="flex gap-4">
        <Link href="/auth/login" className="text-blue-600 underline">
          Đăng nhập
        </Link>
        <Link href="/auth/register" className="text-blue-600 underline">
          Đăng ký
        </Link>
      </div>
    </main>
  );
}
