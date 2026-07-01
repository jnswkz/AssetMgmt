# AssetMgmt — IT Asset Management API

English | [Tiếng Việt](#tiếng-việt)

A single-project ASP.NET Core 8 Web API for managing the full lifecycle of
company hardware: procurement, allocation, maintenance, depreciation, and
disposal. Three roles (Employee, Manager, AdminIT) with JWT authentication.

## Tech stack

- ASP.NET Core 8 Web API (single project)
- SQL Server 2022 + Entity Framework Core 8 (database-first)
- JWT Bearer auth + BCrypt password hashing
- Hangfire (background jobs: lock timeout, depreciation)
- QuestPDF (handover PDF), QRCoder (QR codes)
- FluentValidation (request validation)
- Scalar (OpenAPI documentation UI)

## Requirements

- .NET SDK 8.0
- SQL Server 2022 (local, RDS, or the Docker Compose stack below)

## Configuration

The app reads settings from environment variables (or a `.env` file in the
project root). Copy the example and fill in your values.

```bash
cp .env.example .env
```

Key variables:

| Variable | Description |
|---|---|
| `DB_SERVER` | SQL Server host |
| `DB_PORT` | Port (default 1433) |
| `DB_NAME` | Database name (`AssetMgmt`) |
| `DB_USER` / `DB_PASSWORD` | SQL login |
| `DB_TRUST_CERT` | Trust server certificate |
| `JWT_SECRET` | JWT signing key (>= 32 chars) |
| `SEED_DEFAULT_PASSWORD` | Default password for seeded users |

## Database setup

This project is database-first. Run the initialization script once against your
SQL Server. It creates the databases, schemas, tables, and seed data
(departments, users, 50 demo assets, depreciation policies).

1. Run `VDTPlan/database-init.sql`
2. Run `Migrations/manual/001_asset_disposals.sql`

On first startup, the app replaces the placeholder password hashes on the seeded
users with a real hash, so the demo accounts can log in.

## Run locally

```bash
dotnet restore
dotnet run
```

The API listens on `http://localhost:5046` by default.

- OpenAPI docs (Scalar): `http://localhost:5046/scalar/v1`
- Hangfire dashboard: `http://localhost:5046/hangfire`

## Run with Docker

Starts SQL Server, initializes the database once, then starts the API.

```bash
docker compose up --build
```

The API is available at `http://localhost:8080`. Set `SA_PASSWORD` and
`JWT_SECRET` via a `.env` file or the shell to override the defaults.

## Demo accounts

All seeded accounts use the default password (`Password123!` unless overridden).

| Username | Role |
|---|---|
| `admin` | AdminIT |
| `manager1`, `manager2` | Manager |
| `emp1`, `emp2`, `emp3` | Employee |

## Roles

- **Employee** — browse assets, request an asset, view own requests and assets.
- **Manager** — Employee access plus approve/reject requests, asset lifecycle
  (return, transfer, maintenance, disposal), reports, departments.
- **AdminIT** — full access, including user and department management.

## API overview

Base path: `/api`. All endpoints except login require a Bearer token.

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me` |
| Asset models | `GET/POST /asset-models`, `GET/PUT/DELETE /asset-models/{id}` |
| Asset instances | `GET/POST /assets`, `GET/PUT/DELETE /assets/{id}` |
| Asset lifecycle | `POST /assets/{id}/return`, `/transfer`, `/maintenance`, `/dispose` |
| Requests | `POST /requests`, `GET /requests/pending`, `GET /requests/mine`, `POST /requests/{id}/approve`, `/reject`, `GET /requests/{id}/handover` |
| Allocations | `GET /allocations/history`, `GET /assets/{id}/history`, `GET /me/assets` |
| Disposals | `GET /disposals` |
| Users | `GET/POST /users`, `GET/PUT/DELETE /users/{id}`, `POST /users/{id}/reset-password` |
| Departments | `GET/POST /departments`, `GET/PUT/DELETE /departments/{id}`, `POST /departments/{id}/manager` |
| Reports | `GET /reports/dashboard`, `GET /reports/idle-assets` |

For the full request/response schema, use the Scalar UI at `/scalar/v1`.

## Background jobs

Hangfire runs two recurring jobs:

- **Lock timeout** (every 5 minutes) — releases expired temporary locks on
  pending requests back to stock.
- **Depreciation** (monthly) — posts one depreciation ledger row per asset that
  has a depreciation policy.

Trigger them manually from the Hangfire dashboard at `/hangfire`.

## Project structure

```
Domain/          Entities and enums
Application/     Services, DTOs, validation
Infrastructure/  EF Core, persistence, background jobs, external services
Controllers/     API endpoints
Middleware/      Exception handling, audit logging
VDTPlan/         Database scripts and planning docs
```

---

# Tiếng Việt

[English](#assetmgmt--it-asset-management-api) | Tiếng Việt

API xây dựng bằng ASP.NET Core 8 (một project) để quản lý toàn bộ vòng đời tài
sản phần cứng của công ty: mua sắm, cấp phát, bảo trì, khấu hao và thanh lý.
Ba vai trò (Employee, Manager, AdminIT), xác thực bằng JWT.

## Công nghệ

- ASP.NET Core 8 Web API (một project)
- SQL Server 2022 + Entity Framework Core 8 (database-first)
- Xác thực JWT Bearer + mã hóa mật khẩu BCrypt
- Hangfire (tác vụ nền: hết hạn khóa, khấu hao)
- QuestPDF (PDF biên bản bàn giao), QRCoder (mã QR)
- FluentValidation (kiểm tra dữ liệu request)
- Scalar (giao diện tài liệu OpenAPI)

## Yêu cầu

- .NET SDK 8.0
- SQL Server 2022 (local, RDS, hoặc stack Docker Compose bên dưới)

## Cấu hình

Ứng dụng đọc cấu hình từ biến môi trường (hoặc file `.env` ở thư mục gốc).
Sao chép file mẫu và điền giá trị của bạn.

```bash
cp .env.example .env
```

Các biến chính:

| Biến | Mô tả |
|---|---|
| `DB_SERVER` | Địa chỉ SQL Server |
| `DB_PORT` | Cổng (mặc định 1433) |
| `DB_NAME` | Tên database (`AssetMgmt`) |
| `DB_USER` / `DB_PASSWORD` | Tài khoản SQL |
| `DB_TRUST_CERT` | Tin cậy chứng chỉ server |
| `JWT_SECRET` | Khóa ký JWT (>= 32 ký tự) |
| `SEED_DEFAULT_PASSWORD` | Mật khẩu mặc định cho user seed |

## Khởi tạo cơ sở dữ liệu

Dự án theo mô hình database-first. Chạy script khởi tạo một lần trên SQL Server.
Script sẽ tạo database, schema, bảng và dữ liệu mẫu (phòng ban, người dùng,
50 tài sản demo, chính sách khấu hao).

1. Chạy `VDTPlan/database-init.sql`
2. Chạy `Migrations/manual/001_asset_disposals.sql`

Khi khởi động lần đầu, ứng dụng thay thế mật khẩu placeholder của các user seed
bằng hash thật, để các tài khoản demo có thể đăng nhập.

## Chạy trên máy

```bash
dotnet restore
dotnet run
```

API mặc định chạy ở `http://localhost:5046`.

- Tài liệu OpenAPI (Scalar): `http://localhost:5046/scalar/v1`
- Dashboard Hangfire: `http://localhost:5046/hangfire`

## Chạy bằng Docker

Khởi động SQL Server, khởi tạo database một lần, sau đó chạy API.

```bash
docker compose up --build
```

API chạy ở `http://localhost:8080`. Đặt `SA_PASSWORD` và `JWT_SECRET` qua file
`.env` hoặc shell để thay giá trị mặc định.

## Tài khoản demo

Tất cả tài khoản seed dùng mật khẩu mặc định (`Password123!` nếu không đổi).

| Tài khoản | Vai trò |
|---|---|
| `admin` | AdminIT |
| `manager1`, `manager2` | Manager |
| `emp1`, `emp2`, `emp3` | Employee |

## Phân quyền

- **Employee** — xem tài sản, tạo yêu cầu cấp phát, xem yêu cầu và tài sản của
  chính mình.
- **Manager** — quyền của Employee, thêm duyệt/từ chối yêu cầu, quản lý vòng đời
  tài sản (thu hồi, chuyển giao, bảo trì, thanh lý), báo cáo, phòng ban.
- **AdminIT** — toàn quyền, bao gồm quản lý người dùng và phòng ban.

## Tổng quan API

Đường dẫn gốc: `/api`. Mọi endpoint (trừ login) cần Bearer token.

| Nhóm | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me` |
| Mẫu tài sản | `GET/POST /asset-models`, `GET/PUT/DELETE /asset-models/{id}` |
| Tài sản | `GET/POST /assets`, `GET/PUT/DELETE /assets/{id}` |
| Vòng đời tài sản | `POST /assets/{id}/return`, `/transfer`, `/maintenance`, `/dispose` |
| Yêu cầu | `POST /requests`, `GET /requests/pending`, `GET /requests/mine`, `POST /requests/{id}/approve`, `/reject`, `GET /requests/{id}/handover` |
| Cấp phát | `GET /allocations/history`, `GET /assets/{id}/history`, `GET /me/assets` |
| Thanh lý | `GET /disposals` |
| Người dùng | `GET/POST /users`, `GET/PUT/DELETE /users/{id}`, `POST /users/{id}/reset-password` |
| Phòng ban | `GET/POST /departments`, `GET/PUT/DELETE /departments/{id}`, `POST /departments/{id}/manager` |
| Báo cáo | `GET /reports/dashboard`, `GET /reports/idle-assets` |

Xem chi tiết schema request/response tại giao diện Scalar `/scalar/v1`.

## Tác vụ nền

Hangfire chạy hai tác vụ định kỳ:

- **Hết hạn khóa** (mỗi 5 phút) — giải phóng các khóa tạm đã hết hạn của yêu cầu
  đang chờ, trả tài sản về kho.
- **Khấu hao** (hàng tháng) — ghi một dòng sổ khấu hao cho mỗi tài sản có chính
  sách khấu hao.

Có thể chạy thủ công từ dashboard Hangfire tại `/hangfire`.

## Cấu trúc dự án

```
Domain/          Entity và enum
Application/     Service, DTO, validation
Infrastructure/  EF Core, lưu trữ, tác vụ nền, dịch vụ ngoài
Controllers/     Các endpoint API
Middleware/      Xử lý lỗi, ghi audit log
VDTPlan/         Script database và tài liệu kế hoạch
```
