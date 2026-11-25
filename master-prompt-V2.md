# MASTER PROMPT — SURAT GENERATOR WINDOWS V2
# BAHASA INDONESIA

PERAN:
Bertindak sebagai Senior C# .NET Developer,
Software Architect, dan WPF UI/UX Designer.

NAMA PROYEK:
Surat Generator

TUJUAN:
Buat aplikasi desktop Windows untuk menghasilkan
berbagai jenis surat dari template Microsoft Word.

Aplikasi harus:
- Bisa digunakan secara offline.
- Bisa diinstal di banyak komputer Windows.
- Membaca template Microsoft Word (.docx dan .doc dengan konversi otomatis).
- Mendeteksi placeholder secara otomatis.
- Membuat form input tanpa pengaturan manual.
- Menampilkan live preview dokumen.
- Menyimpan hasil dalam Word dan PDF.
- Memiliki pencarian template.
- Mengambil seluruh template dari satu folder lokal yang dapat diatur pengguna.
- Memiliki tampilan modern, simpel, dan animatif.

Semua komputer pengguna memiliki Microsoft Word.

==================================================
TEKNOLOGI YANG DIGUNAKAN
==================================================

Bahasa              : C#
Framework           : .NET 10
Desktop             : WPF
Arsitektur          : MVVM
Library UI          : WPF UI (lepoco)
MVVM Toolkit        : CommunityToolkit.Mvvm
Pembaca Word        : Open XML SDK
Mesin Word          : Microsoft Office Interop Word
Database Lokal      : SQLite + EF Core
Sumber Template     : Folder lokal yang dipindai aplikasi
Preview PDF         : PDF Viewer kompatibel WPF
Installer           : Windows EXE Installer

Jangan gunakan Electron atau web server.
Aplikasi harus benar-benar desktop native.

==================================================
ATURAN WAJIB PENGEMBANGAN
==================================================

1. Kerjakan mulai dari PHASE 0 secara berurutan.
2. Kerjakan hanya SATU PHASE setiap kali.
3. Jangan otomatis melanjutkan phase berikutnya.
4. Setelah phase selesai, jalankan build dan test.
5. Perbaiki seluruh error sebelum melanjutkan.
6. Laporkan hasil pekerjaan secara singkat.
7. Tunggu perintah "LANJUT PHASE BERIKUTNYA".
8. Jangan menghapus fitur yang sudah berfungsi.
9. Gunakan kode bersih, sederhana, dan modular.
10. Hindari dependency yang tidak diperlukan.
11. Gunakan async/await untuk proses berat.
12. Jangan sampai UI mengalami freeze.
13. Jangan mengubah file template Word asli.
14. Jangan membuat fitur atau tombol palsu.
15. Nonaktifkan fitur yang belum selesai.
16. Semua teks UI menggunakan bahasa Indonesia.
17. Nama class, method, dan variabel boleh Inggris.
18. Simpan data pengguna dengan aman.
19. Jangan mengklaim sukses tanpa pengujian.
20. Pastikan aplikasi tetap berjalan offline.

==================================================
PHASE 0 — PEMERIKSAAN & INSTALASI STACK
==================================================

TUJUAN:
Menyiapkan seluruh kebutuhan pengembangan sebelum
mulai membuat aplikasi.

LANGKAH 1 — PERIKSA KOMPUTER

Periksa:
- Versi Windows dan arsitektur prosesor.
- Ketersediaan .NET SDK.
- Ketersediaan .NET 10 SDK.
- Dukungan template WPF.
- NuGet.
- Git.
- Microsoft Word.
- Registrasi COM Microsoft Word.
- Izin akses folder proyek.

Jalankan:
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
git --version

LANGKAH 2 — INSTALASI KEBUTUHAN

Jika belum tersedia:
- Instal .NET 10 SDK dari sumber resmi.
- Instal Git jika diperlukan.
- Siapkan WPF development environment.
- Pastikan NuGet dapat digunakan.
- Jangan instal ulang software yang sudah sesuai.
- Minta persetujuan sebelum instalasi sistem
  yang membutuhkan hak administrator.
- Jangan mengubah konfigurasi sistem lain.

LANGKAH 3 — BUAT STRUKTUR PROYEK

Buat solution:

SuratGenerator.sln
|
+-- SuratGenerator.Desktop
+-- SuratGenerator.Core
+-- SuratGenerator.Documents
+-- SuratGenerator.Infrastructure
+-- SuratGenerator.Tests

Gunakan net10.0-windows untuk proyek yang
memerlukan fitur khusus Windows.

LANGKAH 4 — INSTAL PACKAGE NUGET

Pasang versi stabil dan kompatibel:
- CommunityToolkit.Mvvm
- WPF-UI
- DocumentFormat.OpenXml
- Microsoft.Office.Interop.Word
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.EntityFrameworkCore.Design

Pasang package hanya pada proyek yang membutuhkan.

Package akses folder lokal dan PDF Viewer dipasang
ketika phase terkait dikerjakan.

LANGKAH 5 — VALIDASI

Jalankan:
dotnet restore
dotnet build

Perbaiki semua error.
Pastikan aplikasi WPF bisa dijalankan.

LANGKAH 6 — LAPORAN

Laporkan:
- Sistem operasi.
- Versi .NET SDK.
- Status Microsoft Word.
- Package yang terinstal.
- Hasil build.
- Kendala yang belum selesai.

Setelah selesai, BERHENTI.

==================================================
PHASE 1 — TAMPILAN MODERN & NAVIGASI
==================================================

TUJUAN:
Membuat UI Windows yang simpel, modern,
profesional, dan memiliki animasi halus.

DESAIN:
- Menggunakan Fluent Design Windows 11.
- Minimalis dan tidak terlalu ramai.
- Navigasi mudah dipahami.
- Banyak animasi kecil yang halus.
- Tetap ringan dan responsif.

WARNA:
Biru Utama    : #2563EB
Background    : #F8FAFC
Surface       : #FFFFFF
Teks Utama    : #0F172A
Teks Kedua    : #64748B
Border        : #E2E8F0

FONT:
Segoe UI.

GAYA KOMPONEN:
- Sudut membulat 10–14px.
- Shadow lembut.
- Jarak antarkomponen konsisten.
- Ikon modern.
- Tombol modern.
- Form input rapi.
- Mendukung light mode dan dark mode.
- Tampilan menyesuaikan ukuran jendela.

SIDEBAR:
1. Buat Surat
2. Template
3. Riwayat Surat
4. Draft
5. Pengaturan

Tambahkan indikator Online/Offline.

HALAMAN UTAMA:

Bagian atas:
- Judul "Buat Surat".
- Kolom pencarian template.
- Filter kategori.
- Tombol Upload Template.
- Tombol Perbarui Template.

Bagian pilihan template:
- Daftar template berbentuk card sederhana.
- Nama template.
- Kategori template.
- Ikon favorit.
- Animasi saat template dipilih.

Bagian editor:
Gunakan layout dua kolom.

KIRI (40%):
- Form input otomatis.
- Area bisa di-scroll.
- Label input jelas.
- Validasi input.

KANAN (60%):
- Live preview dokumen Word.
- Zoom In dan Zoom Out.
- Fit Width.
- Scroll halaman dokumen.

BAGIAN BAWAH:
- Simpan Draft
- Reset
- Export Word
- Export PDF
- Print

ANIMASI:
- Fade-in ketika halaman dibuka.
- Slide ketika berpindah menu.
- Hover pada tombol.
- Animasi pemilihan template.
- Loading animation.
- Transisi panel yang halus.
- Durasi sekitar 150–250ms.
- Gunakan WPF Storyboard.
- Hormati pengaturan Reduced Motion Windows.
- Hindari animasi berlebihan yang memperlambat UI.

TEKNIS:
- Gunakan MVVM.
- Gunakan ResourceDictionary untuk styling.
- Gunakan UserControl yang dapat digunakan ulang.
- Jangan membuat style berulang.
- Fitur belum selesai harus dinonaktifkan.
- Jangan menggunakan data dummy sebagai fitur final.

HASIL:
UI modern, navigasi berfungsi, dan layout rapi.

Setelah selesai, BERHENTI.

==================================================
PHASE 2 — TEMPLATE WORD & FORM OTOMATIS
==================================================

TUJUAN:
Membaca template Word dan membuat form input
secara otomatis berdasarkan placeholder.

FITUR:

1. Import file Microsoft Word .docx atau .doc.
2. Simpan salinan template secara lokal.
3. Deteksi placeholder dalam tanda kurung kurawal.

Contoh:
{tanggal surat}
{nomor surat}
{nama perusahaan}
{alamat perusahaan}
{nama pekerjaan}
{nilai penawaran}

4. Buat input field otomatis.
5. Gunakan nama placeholder sebagai label form.
6. Placeholder sama hanya membuat satu input.
7. Semua placeholder sama diperbarui bersamaan.
8. Deteksi dalam paragraf dan tabel.
9. Deteksi dalam header dan footer.
10. Tangani placeholder yang terpecah Word runs.
11. Pertahankan format asli Microsoft Word.
12. Validasi file yang rusak atau tidak didukung.
13. Jangan mengubah template asli.
14. Simpan metadata template secara lokal.

Gunakan Open XML SDK untuk membaca dokumen.
Gunakan Office Interop bila diperlukan untuk
menjaga format ketika mengganti placeholder.

INPUT DEFAULT:
TextBox.

Siapkan kemungkinan pengembangan:
- DatePicker.
- Input Rupiah.
- Dropdown.
- Multiline TextBox.
- Tabel dinamis.

HASIL:
Upload Word -> Deteksi Placeholder -> Form Otomatis.

Setelah selesai, BERHENTI.

==================================================
PHASE 3 — LIVE PREVIEW MICROSOFT WORD
==================================================

TUJUAN:
Menampilkan isi surat secara langsung di sisi kanan
ketika pengguna mengubah form input.

ALUR:
Pengguna mengetik
-> Tunggu 600ms
-> Buat salinan Word sementara
-> Isi placeholder
-> Konversi ke PDF melalui Word Interop
-> Tampilkan hasil di panel preview.

FITUR:
1. Preview diperbarui otomatis.
2. Mendukung dokumen banyak halaman.
3. Zoom In dan Zoom Out.
4. Fit Width.
5. Scroll dokumen.
6. Indikator loading.
7. Pesan error dan tombol Retry.
8. Preview lama tetap terlihat saat loading.
9. Abaikan hasil render yang sudah tidak relevan.
10. Bersihkan file sementara dengan aman.

ATURAN WORD INTEROP:
- Gunakan dedicated STA thread.
- Proses Word COM harus berurutan.
- Kelola COM object dengan benar.
- Jangan mengganggu Word yang dibuka pengguna.
- Jangan menutup proses WINWORD milik pengguna.
- Tangani error COM dengan aman.
- Jangan memblokir UI thread.
- Jangan gunakan Word Interop sebagai Windows Service.

PDF VIEWER:
Gunakan library yang kompatibel dengan WPF.
Periksa lisensi dan kompatibilitas sebelum instalasi.

HASIL:
Input kiri berubah -> Preview kanan ikut berubah.

Setelah selesai, BERHENTI.

==================================================
PHASE 4 — EXPORT, PRINT & DRAFT
==================================================

TUJUAN:
Menyimpan, mencetak, dan mengelola hasil surat.

FITUR:
1. Export Word (.docx).
2. Export PDF (.pdf).
3. Print menggunakan Microsoft Word.
4. Simpan draft lokal.
5. Auto-save draft.
6. Buka dan edit draft.
7. Riwayat surat.
8. Reset form dengan konfirmasi.
9. Dialog Save As.
10. Notifikasi berhasil/gagal.

DATABASE:
SQLite + Entity Framework Core.

TABEL:
- Templates
- TemplateFields
- Drafts
- GeneratedDocuments

Simpan nilai input draft dalam format JSON.

HASIL:
Surat bisa disimpan, diedit kembali, dan dicetak.

Setelah selesai, BERHENTI.

==================================================
PHASE 5 — MANAJEMEN & PENCARIAN TEMPLATE
==================================================

TUJUAN:
Mempermudah pengguna mencari dan mengelola
template surat.

FITUR:
1. Menampilkan seluruh template lokal.
2. Pencarian template secara real-time.
3. Pencarian tidak membedakan huruf besar/kecil.
4. Pencarian berdasarkan sebagian kata.
5. Filter kategori.
6. Template favorit.
7. Urut berdasarkan nama/tanggal.
8. Ubah nama template.
9. Hapus dengan konfirmasi.
10. Metadata versi template.
11. Refresh daftar template lokal.
12. Pencarian tetap berjalan offline.

CONTOH:
Pengguna mengetik "penawaran".

Aplikasi langsung menampilkan semua template
yang memiliki kata "penawaran".

Tidak perlu menekan Enter.

HASIL:
Daftar template cepat, rapi, dan mudah dicari.

Setelah selesai, BERHENTI.

==================================================
PHASE 6 — SUMBER TEMPLATE FOLDER LOKAL
==================================================

TUJUAN:
Menjadikan satu folder di komputer sebagai sumber
seluruh template yang ditampilkan aplikasi.

PENGATURAN:
- Pilih folder template lokal.
- Opsi menyertakan subfolder.
- Status folder dan jumlah template.
- Waktu pindai terakhir.

FITUR:
1. Validasi folder lokal.
2. Pindai semua file .docx dan .doc di folder.
3. Abaikan file sementara Word dan file tersembunyi.
4. Deteksi template baru.
5. Deteksi perubahan isi template.
6. Deteksi file yang dipindah atau di-rename.
7. Deteksi file yang tidak ada lagi di folder.
8. Salin template ke folder data aplikasi.
9. Jangan mengubah atau menghapus file asli.
10. Pertahankan metadata favorit, kategori, dan versi.
11. Tampilkan semua template di halaman Template.
12. Pilih template dari halaman Buat Surat.
13. Tampilkan ringkasan hasil pindai.
14. Pantau perubahan folder dan perbarui template otomatis.
15. Aplikasi tetap berfungsi tanpa internet.

KEAMANAN:
- Jangan menulis ke folder sumber.
- Jangan menghapus file pengguna.
- Batasi akses hanya untuk membaca file template.
- Simpan path sumber di pengaturan lokal aplikasi.

ALUR:

Pengguna:
Letakkan file .docx atau .doc di folder template ->
Buka Pengaturan -> Pilih Folder -> Simpan ->
Buka Buat Surat -> Pilih Template -> Isi field.

Pembaruan:
Tambah atau ubah file di folder ->
Template diperbarui otomatis; Pindai Folder tersedia untuk sinkronisasi manual.

HASIL:
Semua template di satu folder langsung tersedia
di aplikasi tanpa OAuth dan tanpa koneksi internet.

Setelah selesai, BERHENTI.

==================================================
PHASE 7 — PENGUJIAN & INSTALLER WINDOWS
==================================================

TUJUAN:
Membuat aplikasi siap digunakan oleh pengguna.

PENGUJIAN:
- Windows 10 dan Windows 11.
- Microsoft Word terinstal.
- Import template DOCX dan DOC.
- Deteksi placeholder.
- Placeholder dalam tabel.
- Placeholder dalam header/footer.
- Live preview banyak halaman.
- Export Word.
- Export PDF.
- Print.
- Mode offline.
- Sumber template dari folder lokal dengan pembaruan otomatis.
- Penyimpanan draft.
- Riwayat surat.
- Restart aplikasi.
- Resize jendela.
- Penggunaan memori.
- Pembersihan proses Word COM.

OPTIMASI:
- Percepat startup.
- Hindari render yang tidak perlu.
- Optimalkan SQLite.
- Cegah UI freeze.
- Optimalkan animasi.
- Tambahkan error logging.

INSTALLER:
- Build Release x64.
- Publish self-contained bila sesuai.
- Buat installer EXE Windows.
- Sertakan uninstaller.
- Pertahankan data saat upgrade.
- Tambahkan ikon aplikasi.
- Buat shortcut Start Menu.
- Dokumentasikan kebutuhan Microsoft Word.

HASIL AKHIR:
SuratGenerator-Setup.exe
File aplikasi Release
README.md
Panduan instalasi
Panduan penggunaan

Setelah selesai, BERHENTI.

==================================================
KRITERIA KEBERHASILAN
==================================================

Aplikasi wajib:
- Berjalan offline untuk pembuatan surat.
- Menggunakan template Microsoft Word.
- Mendeteksi placeholder otomatis.
- Membuat form input dinamis.
- Memiliki live preview.
- Menjaga format asli dokumen Word.
- Export Word dan PDF.
- Mendukung print.
- Menyimpan draft dan riwayat.
- Memiliki pencarian template.
- Sumber template dari folder lokal dengan pembaruan otomatis.
- Memiliki UI modern dan minimalis.
- Memiliki animasi yang halus.
- Bisa diinstal di komputer Windows lain.
- Berhasil build tanpa error.

==================================================
PERINTAH EKSEKUSI
==================================================

MULAI KERJAKAN PHASE 0 SAJA.

1. Periksa seluruh kebutuhan stack.
2. Instal dependency yang belum tersedia.
3. Buat struktur solution dan project.
4. Instal package NuGet yang diperlukan.
5. Jalankan restore dan build.
6. Perbaiki error jika ditemukan.
7. Berikan laporan singkat hasil pekerjaan.
8. BERHENTI setelah PHASE 0 selesai.

Jangan melanjutkan PHASE 1 secara otomatis.

Tunggu perintah saya:

"LANJUT PHASE BERIKUTNYA"