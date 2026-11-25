# Pengujian

## Bukti yang Sudah Ada

- Windows 10 Pro build 19045 x64 diuji.
- Windows 11 kompatibel sebagai target, tetapi belum diuji langsung pada mesin ini.
- .NET SDK 10.0.404 dan runtime .NET 10.0.15 terpasang.
- Build Release menghasilkan 0 peringatan dan 0 galat.
- 104 pengujian xUnit lulus, termasuk backup/restore Fase 8, hardening Fase 9, duplikat proyek, penghapusan kategori, adopsi dan deduplikasi master, penghapusan dokumen, deteksi folder pekerjaan hilang, progress refresh yang mencapai 100%, daftar template yang mengikuti folder aktif, dan sinkronisasi checksum master yang berubah di luar aplikasi.
- Uji asap navigasi semua halaman lulus tanpa stderr, termasuk marker Panduan, tombol Hapus Master Struktur Folder dan Hapus Dokumen, backup/restore, scroll Pengaturan, dan smoke dari exe terpasang.
- Pemeriksaan UI Buat Surat menampilkan status `Baru 0, diperbarui 0, tidak berubah 22, hilang 0, gagal 0. Menampilkan 22 dari 22 template; 13 template dari folder lain disembunyikan.` setelah path folder aktif dipakai.
- Sinkronisasi checksum master diuji untuk file master yang ditimpa langsung di folder master; pembuatan pekerjaan dan refresh tetap berhasil serta checksum DB mengikuti hash file aktual.
- Proses `WINWORD` tidak ditinggalkan berjalan setelah penggunaan.
- Mode template folder lokal diuji dengan file baru, perubahan isi, penghapusan sumber, file rusak, subfolder, pemindahan nama file, dan konversi `.doc` dengan converter tiruan.
- Backup diuji untuk arsip valid, manifest SHA256, path tidak aman, manipulasi arsip, pilihan proyek, dan backup harian.
- Restore diuji untuk alur stage + restart, tujuan marker tidak valid, file staging dimanipulasi, lock operasi, dan backup ulang saat arsip hilang.

Status `Lulus` hanya dipakai untuk bukti yang sudah ada. `Terbatas` berarti sebagian bukti atau implementasi ada, tetapi verifikasi belum lengkap. `Perlu verifikasi manual` berarti belum ada bukti pada sesi ini.

## Menjalankan Ulang Build dan Tes

Variabel lingkungan berikut harus ditetapkan sebelum `dotnet build` dan `dotnet test`.

```powershell
$env:DOTNET_ROOT = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$env:DOTNET_ROOT_X64 = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet"
$dotnet = "C:\Users\Lenovo\AppData\Local\Microsoft\dotnet\dotnet.exe"
& $dotnet restore SuratGenerator.sln
& $dotnet build SuratGenerator.sln -c Release -p:Platform=x64
& $dotnet test SuratGenerator.sln -c Release -p:Platform=x64 --no-build
```

Gunakan `-p:Platform=x64` agar seluruh assembly test dibangun sebagai x64 dan seluruh pengujian ikut berjalan.

Untuk memeriksa rilis self-contained, tetapkan kembali kedua variabel lalu jalankan publish dengan target `win-x64` dan `--self-contained true`.

## Matriks Pengujian

| Area | Status | Bukti/Catatan |
| --- | --- | --- |
| Windows 10 | Lulus | Windows 10 Pro build 19045 x64 diuji langsung. |
| Windows 11 | Terbatas | Kompatibel sebagai target, belum diuji langsung pada mesin ini. |
| Microsoft Word terinstal | Lulus | Word 2016+ tersedia; COM Word dapat diinisialisasi dan `WINWORD` tidak ditinggalkan berjalan. |
| Folder template lokal | Lulus | `FolderTemplateTests` dan `TemplateFolderScopeTests` memverifikasi impor, pindai ulang, perubahan isi, file hilang, file rusak, subfolder, pemindahan file, impor `.doc` tanpa mengubah file asli, serta template dari folder lama yang disembunyikan saat path aktif diganti. |
| Konversi `.doc` | Terbatas | Converter tiruan diuji otomatis; konversi Word COM asli perlu verifikasi manual pada folder pengguna. |
| Auto-refresh folder | Perlu verifikasi manual | `TemplateFolderWatcher` memantau perubahan dengan debounce, tetapi belum ada bukti otomatisasi pada sesi ini. |
| Pencarian template | Lulus | `TemplateQueryServiceTests` memverifikasi pencarian nama, nama file sumber, dan kategori; filter dipakai di halaman Template dan Buat Surat. |
| Deteksi placeholder termasuk tabel, header, footer | Lulus | `TemplateReaderTests` memverifikasi paragraf, tabel, header, footer, run terpecah, dan deduplikasi placeholder. |
| Pratinjau banyak halaman | Perlu verifikasi manual | Kemampuan tersedia, tetapi belum ada bukti dokumen banyak halaman pada sesi ini. |
| Ekspor Word | Perlu verifikasi manual | Implementasi ekspor `.docx` ada, belum ada bukti ekspor berhasil. |
| Ekspor PDF | Perlu verifikasi manual | Implementasi ekspor `.pdf` ada, belum ada bukti ekspor berhasil. |
| Cetak ke printer fisik | Terbatas | Alur cetak tersedia, tetapi belum dicetak ke kertas pada printer fisik. |
| Mode offline | Lulus | Seluruh alur memakai folder lokal dan tidak memerlukan koneksi internet. |
| Draft | Lulus | Pengujian xUnit penyimpanan dan pembaruan draft lulus. |
| Riwayat surat | Lulus | Pengujian xUnit pencatatan dokumen hasil lulus. |
| Restart aplikasi | Perlu verifikasi manual | Belum ada bukti draft, riwayat, dan pengaturan tetap terbaca setelah restart. |
| Resize jendela | Lulus | Uji asap mengubah jendela ke 1000x640 lalu 1200x720 tanpa galat. |
| Penggunaan memori | Terbatas | Working set 233,8 MB pada smoke exe terpasang 1.0.9; ambang memori belum ditetapkan. |
| Pembersihan proses Word COM | Lulus | `WINWORD` tidak ditinggalkan berjalan setelah pengujian. |
| Build | Lulus | 0 peringatan dan 0 galat pada build Release x64. |
| Tes otomatis | Lulus | 104 pengujian xUnit lulus, termasuk master baru yang ditarik saat refresh, adopsi file existing, deduplikasi salinan identik, peringatan salinan beda isi, penghapusan dokumen, folder pekerjaan hilang, penghapusan metadata proyek, progress refresh yang mencapai 100% sebelum indikator disembunyikan, template dari folder lama yang tidak ikut ditampilkan, dan sinkronisasi otomatis checksum master yang berubah di luar aplikasi saat create maupun refresh. |
| Backup dan restore | Lulus | `Phase8BackupTests` dan `Phase9BackupTests` memverifikasi arsip, checksum, path traversal, stage + restart, seleksi proyek, lock, error restore, dan backup harian. |
| Navigasi UI | Lulus | Semua halaman dapat dinavigasi tanpa stderr; detail pekerjaan dapat dibuka dan kembali serta memiliki tombol `Hapus` dokumen (`DOKUMEN_HAPUS_OK`); Panduan memiliki marker `Fungsi Menu`, `Alur Cepat`, dan `Backup dan Restore`; Master Struktur Folder memiliki tombol `Hapus`; Pengaturan memiliki marker folder backup, `Buat Backup Sekarang`, dan `Siapkan Restore + Restart`. |
| Scroll Detail Pekerjaan, Pengaturan, dan Panduan | Lulus | Uji asap memakai wheel nyata pada `ProjectDetailScrollViewer`, `SettingsScrollViewer`, dan `GuideScrollViewer` sampai marker `Field Profil`, tombol `Siapkan Restore + Restart`, serta marker `Backup dan Restore` berada di dalam area jendela (`PROJECTDETAIL_SCROLL_OK`, `PENGATURAN_SCROLL_OK`, `PANDUAN_SCROLL_OK`). |
| Scroll kartu Buat Surat | Lulus | Wheel di Data Surat menggeser field dari Y 514 ke 466; posisi Y tombol `Simpan Draft` tetap 810 setelah wheel di Data Surat dan di Pratinjau. |
| Installer | Lulus | `SuratGenerator-Setup.exe` 1.0.9 dibangun dari kode terbaru, uninstall 1.0.8 dan install baru berhasil dengan exit code 0, registry `Surat Generator 1.0.9`, data pengguna dipertahankan (44 file, hash database `508CC271DED12B07F06AA938657C83B2E2332C5D7E1F61A4D36EB80D77CDB10B` sama sebelum/sesudah), shortcut desktop dibuat, dan smoke lulus dari exe terpasang. SHA256: `A7942E9B547CD6BD4C4E2917F85F933A7FFD94F94A183AEA68B6EDE36C769998`. |
| Shortcut desktop | Lulus | `C:\Users\Lenovo\Desktop\Surat Generator.lnk` dibuat dan mengarah ke `C:\Users\Lenovo\AppData\Local\Programs\SuratGenerator\SuratGenerator.Desktop.exe`. |

## Lokasi Log

Log kesalahan ditulis ke `%LOCALAPPDATA%\SuratGenerator\Logs\surat-generator.log` dan dipakai sebagai lokasi troubleshooting. Gunakan pesan status di antarmuka sebagai informasi cepat, lalu periksa log bila diperlukan.
