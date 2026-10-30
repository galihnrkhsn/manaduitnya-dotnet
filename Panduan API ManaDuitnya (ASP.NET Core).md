# Panduan API ManaDuitnya (ASP.NET Core)

Daftar endpoint yang perlu disiapkan, berdasarkan DDL final PostgreSQL (users, categories, wallets, budgets, transactions, refresh\_tokens). Dibuat sederhana, tanpa endpoint yang belum perlu.

## Urutan pengerjaan yang disarankan

Auth → Wallets → Categories → Transactions → Budgets → Reports

## 1. Auth

| Endpoint | Fungsi |
| --- | --- |
| `POST /auth/register` | Buat user baru |
| `POST /auth/login` | Cek email + password, kembalikan access token + refresh token |
| `POST /auth/refresh` | Tukar refresh token dengan access token baru |
| `POST /auth/logout` | Set `revoked_at` pada refresh token |
| `GET /auth/me` | Data profil user yang sedang login |
| `PUT /auth/me` | Ubah nama |
| `PUT /auth/change-password` | Ganti password |

Catatan:

- Di `refresh_tokens`, simpan **hash** token (SHA-256, 64 karakter), jangan token aslinya.
- Lakukan rotasi: setiap refresh, token lama di-revoke dan dibuatkan yang baru.
- Supaya user tidak auto-logout, beri masa berlaku refresh token panjang dan perpanjang tiap dipakai.
- `email_verified_at` dan `remember_token` peninggalan Laravel, boleh diabaikan dulu.

## 2. Wallets

| Endpoint | Fungsi |
| --- | --- |
| `GET /wallets` | Semua dompet milik user, sertakan **saldo saat ini**; opsi `includeArchived` |
| `GET /wallets/{id}` | Detail satu dompet |
| `POST /wallets` | Buat dompet (nama + `initial_balance`) |
| `PUT /wallets/{id}` | Ubah nama / saldo awal |
| `POST /wallets/{id}/archive` | Isi `archived_at` |
| `POST /wallets/{id}/unarchive` | Kosongkan `archived_at` |

Saldo tidak disimpan di tabel, hitung dari: `initial_balance + income − expense + transfer masuk − transfer keluar`.

## 3. Categories

| Endpoint | Fungsi |
| --- | --- |
| `GET /categories` | Semua kategori; filter `type` (1 income, 2 expense) dan `includeArchived` |
| `GET /categories/{id}` | Detail |
| `POST /categories` | Buat (name, type, color) |
| `PUT /categories/{id}` | Ubah name / color |
| `POST /categories/{id}/archive` | Arsipkan |
| `POST /categories/{id}/unarchive` | Aktifkan lagi |

Catatan:

- Tidak ada hard delete karena transaksi memakai `ON DELETE RESTRICT`; cukup arsip.
- Nama kembar dalam user + type yang sama (unique constraint) kembalikan **409 Conflict**.
- Sebaiknya `type` tidak boleh diubah setelah kategori dipakai transaksi.

## 4. Transactions

| Endpoint | Fungsi |
| --- | --- |
| `GET /transactions` | List dengan paging; filter `from`, `to`, `walletId`, `categoryId`, `type`, `search` (note) |
| `GET /transactions/{uuid}` | Detail |
| `POST /transactions` | Buat income / expense / transfer |
| `PUT /transactions/{uuid}` | Ubah |
| `DELETE /transactions/{uuid}` | Hapus (tabel ini tidak punya `archived_at`, jadi hard delete) |

Aturan validasi di `POST` / `PUT`:

- `amount` > 0.
- `wallet_id`, `to_wallet_id`, dan `category_id` harus milik user yang login.
- Dompet tidak boleh yang sudah diarsipkan.
- Type 1 / 2: `category_id` wajib, tipe kategori harus sama dengan tipe transaksi, `to_wallet_id` harus kosong.
- Type 3 (transfer): `to_wallet_id` wajib dan tidak boleh sama dengan `wallet_id`, `category_id` harus kosong.
- Terima `uuid` dari client (dibuat di PWA) supaya kalau request terkirim dua kali tidak jadi data ganda; kalau uuid sudah ada, kembalikan data yang sama.

## 5. Budgets

| Endpoint | Fungsi |
| --- | --- |
| `GET /budgets?month=2026-10` | Anggaran per bulan, sertakan **terpakai** dan **sisa** per kategori |
| `GET /budgets/{id}` | Detail |
| `PUT /budgets` | Upsert berdasarkan (category\_id, month, amount) |
| `DELETE /budgets/{id}` | Hapus anggaran |
| `POST /budgets/copy` | Salin anggaran bulan lalu ke bulan ini (opsional, praktis) |

Catatan:

- Karena ada unique (user, category, month), lebih mudah satu endpoint upsert daripada Create dan Update terpisah.
- Hanya kategori expense yang bisa dianggarkan.
- Simpan `month` selalu tanggal 1.

## 6. Reports (untuk dashboard, hanya baca)

| Endpoint | Fungsi |
| --- | --- |
| `GET /reports/summary?from=&to=` | Total pemasukan, pengeluaran, selisih, total saldo semua dompet |
| `GET /reports/by-category?from=&to=&type=` | Total per kategori (untuk grafik) |
| `GET /reports/monthly?year=2026` | Pemasukan dan pengeluaran per bulan (untuk perencanaan tahunan) |
| `GET /reports/daily?month=2026-10` | Total per hari dalam satu bulan |

Transfer jangan dihitung sebagai pemasukan atau pengeluaran di laporan.

## Aturan umum semua endpoint

- `user_id` selalu diambil dari claim JWT, jangan pernah dari body atau URL.
- Setiap query wajib ada `WHERE user_id = @userId`; data milik user lain kembalikan **404**, bukan 403.
- Jadikan type (1 / 2 / 3) sebagai `enum` atau konstanta di kode agar tidak ada angka ajaib.
- Format error seragam (misal `ProblemDetails`): 400 validasi, 401 belum login, 404 tidak ditemukan, 409 duplikat.
- Rupiah disimpan `int8` tanpa desimal; kirim ke client sebagai angka bulat.
- Untuk list besar (terutama transaksi), selalu pakai paging (`page`, `pageSize`).
