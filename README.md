# Surat Generator

Surat Generator adalah aplikasi desktop Windows untuk membuat surat dari template Microsoft Word (`.docx`; `.doc` juga didukung melalui konversi otomatis). Proses pembuatan surat, pengelolaan template, draft, dan riwayat dapat digunakan tanpa internet. Microsoft Word 2016+ wajib terpasang untuk pratinjau PDF, ekspor Word, cetak, dan konversi `.doc`.

## Fitur Utama

- Menentukan satu folder template di komputer sebagai sumber semua template.
- Memindai folder `.docx` dan `.doc` untuk menambah template baru, memperbarui file yang berubah, dan mendeteksi file yang hilang; file asli tidak diubah.
- File `.doc` dikonversi otomatis ke `.docx` sementara dengan Microsoft Word, lalu hasil konversi dibersihkan setelah dibaca.
- Memantau folder template secara otomatis sehingga perubahan muncul tanpa menekan tombol pindai; tombol pindai manual tetap tersedia.
- Mendukung folder lokal, Google Drive for Desktop, OneDrive, atau folder jaringan selama dapat dibaca sebagai folder Windows.
- Deteksi placeholder `{nama field}` di paragraf, tabel, header, footer, dan teks yang terpecah menjadi beberapa Word run.
- Form input otomatis tanpa pengaturan manual; placeholder yang sama cukup diisi sekali.
- Pratinjau dokumen langsung dengan dukungan banyak halaman, zoom, zoom out, dan Fit Width.
- Ekspor ke `.docx` dan `.pdf`, serta cetak melalui Microsoft Word.
- Simpan draft manual dan otomatis; buka kembali draft dari halaman Draft.
- Riwayat surat hasil ekspor dan cetak, dengan aksi membuka file atau folder.
- Pencarian template real-time di halaman Template dan Buat Surat, dilengkapi filter kategori, pengurutan, favorit, dan ubah metadata.
- Backup manual dan harian untuk database, pengaturan, template, master dokumen, dan proyek; arsip diverifikasi dengan manifest SHA256.
- Restore dengan pratinjau isi arsip, konfirmasi konflik, backup kondisi saat ini, dan penyelesaian setelah restart aplikasi.

## Teknologi

- WPF .NET 10, arsitektur MVVM dengan CommunityToolkit.Mvvm.
- WPF-UI untuk tampilan modern.
- Open XML SDK untuk membaca dan mendeteksi placeholder pada DOCX.
- EF Core SQLite untuk database lokal.
- Microsoft Word melalui COM late-bound, tanpa PIA.

## Kebutuhan

- Windows 10 x64 atau Windows 11 x64.
- Microsoft Word 2016+ untuk pratinjau PDF, ekspor Word, cetak, dan konversi template `.doc`.
- Tidak memerlukan koneksi internet.
- Rilis self-contained `win-x64` tidak memerlukan penginstalan runtime .NET pada komputer pengguna.

## Lokasi Data

| Jenis | Lokasi |
| --- | --- |
| Database | `%LOCALAPPDATA%\SuratGenerator\surat-generator.db` |
| Template tersalin | `%LOCALAPPDATA%\SuratGenerator\Templates` |
| Pengaturan folder template | `%LOCALAPPDATA%\SuratGenerator\template-folder.json` |
| Pengaturan lokasi kerja | `%LOCALAPPDATA%\SuratGenerator\workspace-paths.json` |
| Master dokumen | `%LOCALAPPDATA%\SuratGenerator\MasterDocuments` |
| Hasil pekerjaan | `%LOCALAPPDATA%\SuratGenerator\Projects` |
| Backup | `%LOCALAPPDATA%\SuratGenerator\Backups` |
| Restore yang menunggu restart | `%LOCALAPPDATA%\SuratGenerator\PendingRestore` |
| Token Google Drive | `%LOCALAPPDATA%\SuratGenerator\secure\drive-token.bin` (tidak disertakan dalam backup) |
| Pratinjau | `%LOCALAPPDATA%\SuratGenerator\Preview` |
| Draft | `%LOCALAPPDATA%\SuratGenerator\Draft` |
| Hasil | `%LOCALAPPDATA%\SuratGenerator\Generated` |
| Log | `%LOCALAPPDATA%\SuratGenerator\Logs\surat-generator.log` |

File log tersedia di `%LOCALAPPDATA%\SuratGenerator\Logs\surat-generator.log` dan dipakai sebagai lokasi pemecahan masalah. Log otomatis berputar pada batas 2 MB dan menyembunyikan nilai sensitif.

## Instalasi

Jalankan `SuratGenerator-Setup.exe` bila tersedia. Installer berbasis Inno Setup menghasilkan rilis self-contained `win-x64`, pintasan Start Menu, dan uninstaller. Data pengguna dipertahankan saat upgrade dan uninstall; hapus folder data secara manual hanya jika memang ingin menghapusnya permanen.

Lihat `docs/PANDUAN-INSTALASI.md` untuk langkah lengkap dan penyiapan folder template.

## Build dari Sumber

Gunakan PowerShell. Variabel lingkungan berikut harus ditetapkan sebelum `dotnet build`, `dotnet test`, atau `dotnet publish`.

```powershell
$env:DOTNET_ROOT = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$dotnet = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet\dotnet.exe"
& $dotnet restore SuratGenerator.sln
& $dotnet build SuratGenerator.sln -c Release -p:Platform=x64
& $dotnet test SuratGenerator.sln -c Release -p:Platform=x64 --no-build
& $dotnet publish SuratGenerator.Desktop\SuratGenerator.Desktop.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o publish\win-x64
```

## Status Validasi

- Windows 10 Pro build 19045 x64 diuji; SDK 10.0.404 dan runtime 10.0.15.
- Build: 0 peringatan dan 0 galat.
- 107 pengujian xUnit lulus, termasuk backup/restore Fase 8 (termasuk pemulihan catatan), hardening Fase 9, duplikat proyek, penghapusan kategori, adopsi dan deduplikasi master dokumen saat refresh, penghapusan dokumen, deteksi folder pekerjaan hilang, progress refresh yang mencapai 100%, daftar template yang mengikuti folder aktif, dan sinkronisasi checksum master yang berubah di luar aplikasi.
- Uji asap UI pada semua halaman lulus tanpa stderr, termasuk menu Catatan, marker Panduan, tombol Hapus Master Struktur Folder dan Hapus Dokumen, backup/restore, scroll Detail Pekerjaan, Pengaturan, dan Panduan dengan wheel nyata, serta smoke dari exe terpasang.
- Proses `WINWORD` tidak ditinggalkan berjalan.
- Windows 11 kompatibel sebagai target, tetapi belum diuji langsung pada mesin ini.
- Installer 1.0.11 dibangun dari kode terbaru, 1.0.10 di-uninstall silent, 1.0.11 terpasang silent dengan shortcut desktop, data pengguna dipertahankan (47 file, hash database sama), smoke dan shortcut Ctrl+Enter pada empat dialog Catatan lulus dari exe terpasang; SHA256 `858A846751458823300E6CB6F5E0FDD9013FC1E404D349EB6014AEC409F482EE`.

Rincian dan batasannya ada di `docs/PENGUJIAN.md`.

## Dokumentasi

- `docs/PANDUAN-INSTALASI.md`
- `docs/PANDUAN-PENGGUNAAN.md`
- `docs/PENGUJIAN.md`
