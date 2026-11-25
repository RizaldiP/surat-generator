# Panduan Instalasi

## Kebutuhan Sistem

- Windows 10 x64 atau Windows 11 x64.
- Microsoft Word 2016+ jika memakai pratinjau PDF, ekspor Word, cetak, atau template `.doc`.
- Tidak memerlukan koneksi internet untuk memakai aplikasi.
- Tidak perlu memasang runtime .NET karena rilis self-contained `win-x64` menyertakan runtime yang dibutuhkan.

## Instalasi untuk Pengguna

1. Dapatkan `SuratGenerator-Setup.exe` hasil rilis.
2. Jalankan installer. Instalasi per pengguna tidak memerlukan hak administrator.
3. Selesaikan wizard instalasi. Installer akan membuat pintasan Start Menu.
4. Buka Surat Generator dari Start Menu.
5. Buka Pengaturan, pilih folder template, lalu simpan. Pemindaian berjalan otomatis; tombol pindai tetap tersedia.

`SuratGenerator-Setup.exe` dibuat dengan Inno Setup dari hasil publish self-contained `win-x64`. Installer menyediakan uninstaller. Saat upgrade atau uninstall, data di `%LOCALAPPDATA%\SuratGenerator\` tidak dihapus otomatis. Jika ingin menghapus data permanen, lakukan manual setelah aplikasi tidak digunakan.

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

Untuk troubleshooting, periksa `%LOCALAPPDATA%\SuratGenerator\Logs\surat-generator.log`. Log ini otomatis berputar pada batas 2 MB dan menyembunyikan nilai sensitif seperti token kata sandi.

## Penyiapan Folder Template

1. Siapkan satu folder yang berisi file template `.docx` atau `.doc`. Folder boleh lokal, hasil sinkron Google Drive for Desktop, OneDrive, atau folder jaringan.
2. Buka halaman **Pengaturan**.
3. Tekan **Pilih Folder**, lalu pilih folder tersebut.
4. Aktifkan **Sertakan subfolder** bila diperlukan.
5. Tekan **Simpan**. Aplikasi menyimpan pengaturan dan langsung memindai isi folder.
6. Perubahan folder terdeteksi otomatis dalam beberapa detik. Gunakan **Pindai Sekarang** bila ingin memaksa sinkronisasi manual.

Aplikasi hanya membaca dan menyalin file. File asli di folder tidak diubah atau dihapus. Template `.doc` dikonversi otomatis ke `.docx` sementara dengan Microsoft Word dan dibersihkan setelah dibaca. Jika folder berada di layanan cloud, sinkronisasi file dilakukan oleh aplikasi layanan cloud tersebut, bukan oleh Surat Generator.

## Build dan Publish dari Sumber

Siapkan .NET SDK 10 dan WPF workload. Variabel lingkungan berikut harus ditetapkan sebelum `dotnet build`, `dotnet test`, atau `dotnet publish`.

```powershell
$env:DOTNET_ROOT = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$dotnet = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet\dotnet.exe"
& $dotnet restore SuratGenerator.sln
& $dotnet build SuratGenerator.sln -c Release -p:Platform=x64
& $dotnet test SuratGenerator.sln -c Release -p:Platform=x64 --no-build
& $dotnet publish SuratGenerator.Desktop\SuratGenerator.Desktop.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o publish\win-x64
```

Gunakan `-p:Platform=x64` agar seluruh assembly test dibangun sebagai x64 dan seluruh pengujian ikut berjalan.

Hasil publish self-contained tidak memerlukan runtime .NET terpasang pada komputer pengguna. Inno Setup digunakan untuk membungkus hasil publish menjadi `SuratGenerator-Setup.exe`.
