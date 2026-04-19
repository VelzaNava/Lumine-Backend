# Lumine Backend

ASP.NET Core REST API backend for the **Lumine AR Jewelry Try-On App** — a thesis project that combines MediaPipe hand/face tracking with Unity 3D rendering for an augmented reality jewelry try-on experience on Android.

---

## Tech Stack

- **Framework**: ASP.NET Core 9 (C#)
- **Database & Auth**: [Supabase](https://supabase.com) (PostgreSQL + GoTrue Auth + Storage)
- **API Docs**: Scalar (available at `/scalar/v1` when running)
- **ORM**: postgrest-csharp (via Supabase SDK)

---

## Features

| Endpoint Group | What it does |
|---|---|
| `api/auth` | Register, login, OTP email verification |
| `api/profile` | View/edit user profile, avatar upload |
| `api/jewelry` | Full CRUD for jewelry catalog, image upload to Supabase Storage |
| `api/jewelry/upload-image` | Accepts multipart image from Android, uploads to Supabase `jewelry` bucket, returns public URL |
| `api/admin` | User management, view favorites |
| `api/evaluation` | Submit AR try-on ratings (1–5 stars) |

---

## Getting Started

### 1. Clone the repo

```bash
git clone https://github.com/VelzaNava/LumineBackend.git
cd LumineBackend
```

### 2. Set up configuration

Copy the example config and fill in your Supabase credentials:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json`:

```json
{
  "Supabase": {
    "Url": "https://your-project-id.supabase.co",
    "Key": "your-service-role-key",
    "AnonKey": "your-anon-key"
  },
  "AdminEmails": [ "your-admin-email@example.com" ]
}
```

> `appsettings.json` is gitignored — never commit your real keys.

### 3. Run

```bash
dotnet run
```

API runs at `http://localhost:5111`.
API explorer at `http://localhost:5111/scalar/v1`.

---

## Supabase Tables Required

Run these in the Supabase SQL editor:

```sql
-- User profiles
create table public.user_profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  first_name text,
  last_name text,
  phone text,
  avatar_url text
);

-- Favorites
create table public.favorites (
  id uuid primary key default gen_random_uuid(),
  user_id uuid references auth.users(id) on delete cascade,
  jewelry_id text not null
);

-- Evaluations
create table public.evaluations (
  id uuid primary key default gen_random_uuid(),
  user_id uuid,
  jewelry_id text,
  jewelry_name text,
  rating int check (rating between 1 and 5),
  comment text,
  created_at timestamptz default now()
);

-- Jewelry
create table public.jewelry (
  id uuid primary key default gen_random_uuid(),
  created_at timestamptz default now(),
  name text,
  type text,
  material text,
  price numeric,
  description text,
  image_url text,
  model_url text,
  is_available boolean default true,
  is_ar_enabled boolean default false
);
```

---

## Project Structure

```
Lumine.Backend/
├── Controllers/
│   ├── AuthController.cs       # Register, login, OTP flow
│   ├── ProfileController.cs    # Profile CRUD, avatar upload
│   ├── AdminController.cs      # User management
│   ├── JewelryController.cs    # Jewelry catalog CRUD
│   └── EvaluationController.cs # AR try-on ratings
├── Models/
│   ├── AuthModels.cs
│   ├── ProfileModels.cs
│   └── EvaluationModels.cs
├── Services/
│   ├── SupabaseService.cs      # Supabase client singleton
│   └── JewelryService.cs       # Jewelry business logic
├── appsettings.example.json    # Config template (safe to commit)
└── appsettings.json            # Real config (gitignored)
```

---

## Android App

The mobile client for this backend is at:
[github.com/VelzaNava/LumineApp](https://github.com/VelzaNava/LumineApp)

---

## Thesis Context

This backend is part of a thesis implementing the **ALBJOA** (Adaptive Landmark-Based Jewelry Overlay Algorithm) using a hybrid Android + Unity architecture with MediaPipe for computer vision.

---

## License

MIT — see [LICENSE](LICENSE)
