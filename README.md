"# PTUDWNC-2026-Nhom17" 
# Culinary Blog – Blog Ẩm thực và Nấu ăn

> **Culinary Blog** là nền tảng ứng dụng web hiện đại cho phép người dùng khám phá, chia sẻ và quản lý các công thức nấu ăn phong phú. Hệ thống được phát triển theo mô hình **API-Driven Architecture**, phân tách độc lập giữa Backend (.NET 10 Clean Architecture) và Frontend (Next.js App Router), đáp ứng các tiêu chuẩn cao về hiệu năng, bảo mật và tối ưu SEO.

---

## Mục lục

- [Giới thiệu Kiến trúc](#giới-thiệu-kiến-trúc)
- [Công nghệ Sử dụng](#công-nghệ-sử-dụng)
- [Tính năng Nổi bật](#tính-năng-nổi-bật)
- [Cấu trúc Dự án](#cấu-trúc-dự-án)
- [Yêu cầu Hệ thống](#yêu-cầu-hệ-thống)
- [Hướng dẫn Cài đặt & Chạy Local](#hướng-dẫn-cài-đặt--chạy-local)
- [Thành viên phát triển dự án](#thành-viên-phát-triển-dự-án)

---

## Giới thiệu Kiến trúc

Hệ thống tuân thủ kiến trúc phân tầng độc lập (**API-Driven Architecture**):

```
┌─────────────────────────────────────────────────────────────────┐
│                    CULINARY BLOG SYSTEM                         │
│                                                                 │
│   ┌──────────────────┐        ┌───────────────────────────────┐ │
│   │  NEXT.JS FRONTEND│◄──────►│    .NET 10 BACKEND API        │ │
│   │  (App Router)    │  REST  │    (Minimal APIs + Clean Arch)│ │
│   │  Port: 3000      │  JSON  │    Port: 5000                 │ │
│   └──────────────────┘        └──────────────┬────────────────┘ │
│                                              │                  │
│   ┌──────┐ ┌────────┐  ┌────────┐  ┌────────┐ ┌───────────┐     │
│   │ Pgsql│ │ Redis  │  │ MinIO  │  │Hangfire│ │Google Auth│     │
│   │:5432 │ │:6379   │  │:9000   │  │ Jobs   │ │ OAuth2.0  │     │
│   └──────┘ └────────┘  └────────┘  └────────┘ └───────────┘     │
└─────────────────────────────────────────────────────────────────┘
```

- **Backend (.NET 10 Minimal APIs)**: Áp dụng **Clean Architecture** (Domain, Application, Infrastructure, Presentation) kết hợp **CQRS pattern** qua MediatR Pipeline.
- **Frontend (Next.js App Router)**: Sử dụng TypeScript, Tailwind CSS, TanStack Query và Auth.js v5. Tối ưu trải nghiệm người dùng kết hợp Server-Side Rendering (SSR) và Incremental Static Regeneration (ISR).
- **Cơ sở dữ liệu & Storage**: PostgreSQL 16 (Full-Text Search), Redis 7 (Distributed Cache), MinIO (Object Storage tương thích S3).

---

## Công nghệ Sử dụng

### **Backend Stack**
- **Framework**: .NET 10 Minimal APIs (C#)
- **Architecture**: Clean Architecture, CQRS (MediatR), Domain-Driven Design concepts
- **ORM & Database**: Entity Framework Core 10, PostgreSQL 16
- **Caching**: Redis 7, Native Output Caching & `IMemoryCache`
- **Security & Authentication**: ASP.NET Core Identity, JWT Stateless (Access Token 15 phút, Refresh Token Rotation 7 ngày), Google OAuth 2.0 (PKCE)
- **Background Jobs**: Hangfire (PostgreSQL Storage)
- **Validation**: FluentValidation + MediatR Pipeline Behavior
- **Observability**: Serilog (Structured Logging), OpenTelemetry (Tracing & Metrics), Health Checks

### **Frontend Stack**
- **Framework**: Next.js 14+ / 15 (App Router), React 19 / TypeScript
- **Styling**: Tailwind CSS
- **State & Data Fetching**: TanStack Query (React Query)
- **Auth**: Auth.js v5 (NextAuth)
- **SEO**: JSON-LD Schema.org Recipe Markup, Open Graph, Dynamic Meta tags

### **Infrastructure & DevOps**
- **Containerization**: Docker, Docker Compose
- **Reverse Proxy**: Nginx Alpine
- **Storage**: MinIO S3-Compatible

---

## Tính năng Nổi bật

Hệ thống bao gồm **27 Yêu cầu Chức năng (FR)** thuộc 7 module chính:

1. **Xác thực & Người dùng (`FR-AUTH`)**:
   - Đăng ký, Đăng nhập Email/Mật khẩu (Auto-login sau đăng ký).
   - Đăng nhập qua Google OAuth 2.0 PKCE.
   - Cơ chế Token Refresh Rotation & Reuse Detection tự động vô hiệu hóa token khi có dấu hiệu tấn công.
   - Quản lý hồ sơ cá nhân (Me Profile).
2. **Quản lý Công thức (`FR-RCP`)**:
   - Tạo, cập nhật, lưu trữ (Archive), xuất bản (Publish) và xóa vĩnh viễn công thức.
   - Quản lý bộ sưu tập hình ảnh (Max 5MB/file, kiểm tra Magic Bytes, chọn Primary Image).
   - Quản lý danh sách nguyên liệu (`RecipeIngredient`) và các bước thực hiện (`RecipeStep` - tự động đánh lại số bước).
   - Xử lý xung đột cập nhật đồng thời bằng `RowVersion` (Optimistic Concurrency).
3. **Quản lý Danh mục (`FR-CAT`)**:
   - CRUD danh mục công thức (Dành riêng cho Admin).
   - Tự động sinh SEO Slug, tích hợp `IMemoryCache` (TTL 60 phút).
4. **Tìm kiếm & Phân trang (`FR-SRCH`)**:
   - Full-Text Search tiếng Việt không dấu sử dụng PostgreSQL `tsvector`/`tsquery` kết hợp extension `unaccent`.
   - Phân trang Offset-based, hỗ trợ lọc theo Category, Độ khó (Difficulty), Thời gian nấu và Sắp xếp linh hoạt.
5. **Quản lý Tệp tin (`FR-FILE`)**:
   - Tầng trừu tượng `IFileStorageService` quản lý Upload/Delete file ảnh công thức trên MinIO S3.
6. **Background Jobs (`FR-JOB`)**:
   - Gửi Email chào mừng người dùng mới (Fire-and-forget qua Hangfire).
   - Tự động tạo Thumbnail (300x300) và Medium (800x600) cho ảnh công thức.
   - Chạy lịch tự động sinh `sitemap.xml` hàng ngày lúc 02:00 AM UTC.
7. **Quan sát Hệ thống (`FR-OBS`)**:
   - Cung cấp 3 Health Check endpoints (`/health`, `/health/live`, `/health/ready`).
   - Structured Logging (Serilog) kèm `CorrelationId` và OpenTelemetry Distributed Tracing.

---

## Cấu trúc Dự án

---

## Yêu cầu Hệ thống

Để khởi chạy dự án ở môi trường phát triển (Development), máy tính của bạn cần cài đặt:


---

## Hướng dẫn Cài đặt & Chạy Local

### **Bước 1: Clone Repository**
```bash
git clone https://github.com/BaoThw05/PTUDWNC-2026-Nhom17.git
```
---
## Thành viên phát triển dự án

| MSSV | Họ và Tên | Vai trò & Mô-đun phụ trách | Hồ sơ Git |
| :---: | :--- | :--- | :---: |
| 2312763 | <nobr>**Trần Lê Bảo Thư**</nobr> | Full-Stack: Module Tìm kiếm Full-Text, SEO, Sitemap & Observability (`FR-SRCH`, `FR-OBS`, `FR-JOB-003`) | [![Git Profile](https://img.shields.io/badge/GitHub-Profile-181717?logo=github)](https://github.com/BaoThw05) |
| 2312793 | <nobr>**Nguyễn Ngọc Tuấn**</nobr> | Full-Stack: Module Xác thực, Người dùng & Welcome Email (`FR-AUTH`, `FR-JOB-001`) | [![Git Profile](https://img.shields.io/badge/GitHub-Profile-181717?logo=github)](https://github.com/Liu-548) |
| 2312776 | <nobr>**Bùi Ngọc Toàn**</nobr> | Full-Stack: Module Công thức Nấu ăn Cốt lõi, Bước & Nguyên liệu (`FR-RCP`) | [![Git Profile](https://img.shields.io/badge/GitHub-Profile-181717?logo=github)](https://github.com/2312776-beep) |
| 2312735 | <nobr>**Lương Đức Sang**</nobr> | Full-Stack: Module Danh mục, Quản lý Tệp tin & Job Resize Ảnh (`FR-CAT`, `FR-FILE`, `FR-JOB-002`) | [![Git Profile](https://img.shields.io/badge/GitHub-Profile-181717?logo=github)](https://github.com/username3) |
