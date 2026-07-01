# AssetMgmt — IT Asset Management API

A single-project ASP.NET Core 8 Web API for managing the full lifecycle of
company hardware: procurement, allocation, maintenance, depreciation, and
disposal. Three roles (Employee, Manager, AdminIT) with JWT authentication.

API cho quản lý vòng đời tài sản CNTT của công ty: mua sắm, cấp phát, bảo trì,
khấu hao và thanh lý. Ba vai trò (Employee, Manager, AdminIT), xác thực bằng JWT.

---

## Tech stack / Công nghệ

- ASP.NET Core 8 Web API (single project)
- SQL Server 2022 + Entity Framework Core 8 (database-first)
- JWT Bearer auth + BCrypt password hashing
- Hangfire (background jobs: lock timeout, depreciation)
- QuestPDF (handover PDF), QRCoder (QR codes)
- FluentValidation (request validation)
- Scalar (OpenAPI documentation UI)

---

## Requirements / Yêu cầu

- .NET SDK 8.0
- SQL Server 2022 (local, RDS, or the Docker Compose stack below)

---

## Configuration / Cấu hình

The app reads settings from environment variables (or a `.env` file in the
project root). Copy the example and fill in your values.

Ứng dụng đọc cấu hình từ biến môi trường (hoặc file `.env` ở thư mục gốc).
Sao chép file mẫu và điền giá trị của bạn.

```bash
cp .env.example .env
```

Key variables / Các biến chính:

| Variable | Description | Mô tả |
|---|---|---|
| `DB_SERVER` | SQL Server host | Địa chỉ SQL Server |
| `DB_PORT` | Port (default 1433) | Cổng (mặc định 1433) |
| `DB_NAME` | Database name (`AssetMgmt`) | Tên database |
| `DB_USER` / `DB_PASSWORD` | SQL login | Tài khoản SQL |
| `DB_TRUST_CERT` | Trust server certificate | Tin cậy chứng chỉ server |
| `JWT_SECRET` | JWT signing key (>= 32 chars) | Khóa ký JWT (>= 32 ký tự) |
| `SEED_DEFAULT_PASSWORD` | Default password for seeded users | Mật khẩu mặc định cho user seed |

---

## Database setup / Khởi tạo cơ sở dữ liệu

This project is database-first. Run the initialization script once against your
SQL Server. It creates the databases, schemas, tables, and seed data
(departments, users, 50 demo assets, depreciation policies).

Dự án theo mô hình database-first. Chạy script khởi tạo một lần trên SQL Server.
Script sẽ tạo database, schema, bảng và dữ liệu mẫu (phòng ban, người dùng,
50 tài sản demo, chính sách khấu hao).

1. Run `VDTPlan/database-init.sql`
2. Run `Migrations/manual/001_asset_disposals.sql`

On first startup, the app replaces the placeholder password hashes on the seeded
users with a real hash, so the demo accounts can log in.

Khi khởi động lần đầu, ứng dụng thay thế mật khẩu placeholder của các user seed
bằng hash thật, để các tài khoản demo có thể đăng nhập.

---

## Run locally / Chạy trên máy

```bash
dotnet restore
dotnet run
```

The API listens on `http://localhost:5046` by default.

API mặc định chạy ở `http://localhost:5046`.

- OpenAPI docs (Scalar): `http://localhost:5046/scalar/v1`
- Hangfire dashboard: `http://localhost:5046/hangfire`

---

## Run with Docker / Chạy bằng Docker

Starts SQL Server, initializes the database once, then starts the API.

Khởi động SQL Server, khởi tạo database một lần, sau đó chạy API.

```bash
docker compose up --build
```

The API is available at `http://localhost:8080`. Set `SA_PASSWORD` and
`JWT_SECRET` via a `.env` file or the shell to override the defaults.

API chạy ở `http://localhost:8080`. Đặt `SA_PASSWORD` và `JWT_SECRET` qua file
`.env` hoặc shell để thay giá trị mặc định.

---

## Demo accounts / Tài khoản demo

All seeded accounts use the default password (`Password123!` unless overridden).

Tất cả tài khoản seed dùng mật khẩu mặc định (`Password123!` nếu không đổi).

| Username | Role |
|---|---|
| `admin` | AdminIT |
| `manager1`, `manager2` | Manager |
| `emp1`, `emp2`, `emp3` | Employee |

---

## Roles / Phân quyền

- **Employee** — browse assets, request an asset, view own requests and assets.
- **Manager** — Employee access plus approve/reject requests, asset lifecycle
  (return, transfer, maintenance, disposal), reports, departments.
- **AdminIT** — full access, including user and department management.

---

## API overview / Tổng quan API

Base path: `/api`. All endpoints except login require a Bearer token.

Đường dẫn gốc: `/api`. Mọi endpoint (trừ login) cần Bearer token.

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

Xem chi tiết schema request/response tại giao diện Scalar `/scalar/v1`.

---

## Background jobs / Tác vụ nền

Hangfire runs two recurring jobs:

Hangfire chạy hai tác vụ định kỳ:

- **Lock timeout** (every 5 minutes) — releases expired temporary locks on
  pending requests back to stock.
- **Depreciation** (monthly) — posts one depreciation ledger row per asset that
  has a depreciation policy.

Trigger them manually from the Hangfire dashboard at `/hangfire`.

Có thể chạy thủ công từ dashboard Hangfire tại `/hangfire`.

---

## Project structure / Cấu trúc dự án

```
Domain/          Entities and enums
Application/     Services, DTOs, validation
Infrastructure/  EF Core, persistence, background jobs, external services
Controllers/     API endpoints
Middleware/      Exception handling, audit logging
VDTPlan/         Database scripts and planning docs
```
