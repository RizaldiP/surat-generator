# INSTRUKSI OPENCODE — PENGEMBANGAN SURAT GENERATOR V3
## Modul Manajemen Pekerjaan, Master Dokumen Fleksibel, Prefill Word, Checklist, dan Backup

> **PENTING: INI ADALAH PENGEMBANGAN APLIKASI YANG SUDAH ADA, BUKAN PERINTAH MEMBUAT ULANG APLIKASI.**
>
> Baca dokumen ini sampai selesai. Kerjakan **satu fase saja setiap kali**, laporkan hasilnya, lalu **BERHENTI** dan tunggu instruksi pengguna `LANJUT PHASE BERIKUTNYA`. Jangan melompati audit awal.

## A. KONTEKS DAN BATASAN WAJIB

Aplikasi target adalah **Surat Generator untuk Windows 10/11**, offline-first. Rancangan sebelumnya memakai C#/.NET 10, WPF, MVVM, SQLite, Open XML SDK, serta integrasi Microsoft Word untuk preview/export. **Jangan mengasumsikan seluruh stack tersebut sudah terpasang atau seluruh fitur sudah dibuat. Audit repository aktual terlebih dahulu.** Gunakan framework, pola MVVM, dependency injection, komponen UI, navigasi, dan layanan yang benar-benar dipakai oleh proyek. Jangan memaksakan migrasi teknologi hanya demi mengikuti dokumen ini.

### ATURAN NON-NEGOSIASI: JANGAN MERUSAK PROGRAM YANG SUDAH ADA

1. **Audit dahulu:** inspeksi solution/project, folder, entry point, dependency, service, model, database/migration, XAML, resource dictionary, theme, navigation, template engine, preview, dan test yang sudah ada. Jangan menebak nama class atau path.
2. **Pertahankan fitur lama:** generator surat, deteksi placeholder `{nama field}`, form otomatis, live preview, template library, export Word/PDF, cetak, draft, riwayat, pengaturan, dan fitur lain yang memang sudah terimplementasi tidak boleh rusak atau hilang.
3. **Jaga UI tetap konsisten:** ikuti style, spacing, typography, palet, animasi, icon, control, responsive layout, sidebar, serta konvensi XAML yang sudah ada. **Jangan mendesain ulang seluruh aplikasi** atau membuat tema kedua. Reuse komponen dan resource yang tersedia.
4. **Jaga arsitektur:** tambah modul melalui struktur proyek yang ada; hindari ViewModel, repository, service, helper, konfigurasi, atau model duplikat. Gunakan dependency injection dan pola error handling yang berlaku di repository.
5. **Perubahan minimal dan terukur:** jangan refactor massal, rename proyek, upgrade dependency besar, hapus file, ubah skema secara destruktif, atau mengganti library preview tanpa alasan kuat dan persetujuan pengguna.
6. **Database aman:** setiap perubahan skema memakai migrasi kompatibel dengan data lama, menyediakan strategi rollback/backup, dan tidak menghapus data pengguna. Jangan otomatis menjalankan operasi destruktif.
7. **Sebelum mengedit:** identifikasi file terdampak, jelaskan pendekatan dan risiko. Jika ditemukan ketidaksesuaian arsitektur, usulkan penyesuaian dan minta persetujuan untuk perubahan besar.
8. **Sesudah setiap fase:** jalankan build dan test yang tersedia, laporkan hasil aktual (jangan mengaku berhasil jika belum diuji), daftar file yang diubah, serta potensi regresi. Perbaiki error yang disebabkan perubahan fase tersebut.
9. **Jangan menghapus integrasi Google Drive yang mungkin sudah ada untuk distribusi Template Surat.** Yang ditiadakan adalah **sinkronisasi Master Dokumen ke Google Drive**. Jangan menambahkan sync cloud untuk Master Dokumen.
10. **Offline-first:** pembuatan pekerjaan, master lokal, pengisian Word, pengelolaan file, checklist, dan backup wajib dapat digunakan tanpa internet.
11. **Jangan langsung mengimplementasikan semua fase dalam satu respons.** Kerjakan fase yang sedang diminta saja, lalu berhenti.

## B. TUJUAN PRODUK

Tambahkan menu **Manajemen Pekerjaan** dan **Master Dokumen** ke Surat Generator yang sudah ada.

Contoh pengguna membuat pekerjaan:
- Nama kapal: `KRI TBT - 516`
- Nama pekerjaan: `Perbaikan Sistem Kelistrikan`
- Lokasi dasar hasil pekerjaan: dapat ditentukan pada Pengaturan, misalnya `D:\Pekerjaan`

Ketika pengguna menekan **Generate Folder**, aplikasi:
1. Memvalidasi input dan mengecek duplikasi.
2. Membuat folder `KRI TBT 516 - Perbaikan Sistem Kelistrikan` (gunakan normalisasi nama folder Windows yang aman; jangan mengubah teks asli yang tersimpan dalam profil).
3. Membuat seluruh kategori folder **aktif** menurut urutan Master Struktur saat itu.
4. Menyalin **semua file master aktif yang terdaftar** ke subfolder kategori masing-masing, **tanpa pemilihan manual**.
5. Menyimpan catatan pekerjaan, snapshot versi struktur dan versi file master yang dipakai, serta status dokumen.
6. Membuka halaman Detail Pekerjaan, tempat pengguna mengisi Word, mengunggah lampiran, melihat checklist, atau membuka File Explorer.

**Jangan mengganti file master saat dokumen pekerjaan diedit.** Master adalah sumber; dokumen pekerjaan adalah salinan independen.

## C. MASTER STRUKTUR DEFAULT (DAPAT DIUBAH)

Berikan struktur awal berikut sebagai **seed/default saat belum ada konfigurasi**, bukan konstanta kaku yang tidak dapat diedit:

```text
01. Dokumen Tagihan/
    Faktur Pembayaran Pekas.docx
    KU 17.docx
    Kuitansi.docx
    Surat Permohonan Pembayaran (SPP).docx
    Surat Kuasa.docx
    Bukti Pemotongan PPh Pasal 23.xlsx
    Format PPh-21 SSP.xlsx
    Format SSP PPh-23 dan PPN.xlsx
02. Efaktur/
03. Undangan Verifikasi/
    Undangan Verifikasi I.docx
    Undangan Verifikasi II.docx
    Undangan Verifikasi III.docx
04. SPMK/
05. JAMLAK/
06. JAMHAR/
07. Pengumuman Tender/
08. Penunjukan Pemenang/
09. Format Verifikasi/
```

**PENTING:** Nama file di atas hanyalah daftar dokumen yang diharapkan, **bukan file template yang tersedia secara otomatis**. Jangan membuat file Word/Excel palsu atau kosong yang seolah-olah template asli. Pengguna akan mengunggah template yang sebenarnya ke Master Dokumen. Saat Generate, hanya file master yang benar-benar tersedia yang disalin. Kategori tanpa file tetap dibuat kosong.

Master Struktur harus mendukung:
- Tambah kategori baru, ubah nama, ubah urutan (drag-and-drop jika komponen existing mendukung; minimal tombol naik/turun), aktif/nonaktif, dan penghapusan aman dengan konfirmasi.
- Tambah/ganti/hapus file master, serta pindah kategori; pertahankan riwayat versi saat mengganti file.
- Nomor urut `01`, `02`, dst. dihitung dari urutan **kategori aktif** ketika proyek dibuat; **ID kategori stabil dan tidak tergantung nomor urut atau nama folder**.
- Validasi nama kategori: tidak kosong, tidak bentrok (case-insensitive), tidak mengandung karakter ilegal Windows, reserved names, atau trailing dot/spasi; penanganan path terlalu panjang.
- Perubahan struktur master hanya memengaruhi **pekerjaan baru**. Jangan otomatis rename/move folder pekerjaan lama atau mengubah arsip lama.
- Jika master aktif diubah saat pekerjaan sedang dibuat, gunakan snapshot yang konsisten.

## D. MASTER DOKUMEN LOKAL

- Lokasi penyimpanan master dan lokasi hasil pekerjaan dapat dikonfigurasi terpisah melalui Pengaturan. Jangan menulis ke `Program Files` atau folder instalasi; gunakan lokasi data pengguna yang writable atau pilihan eksplisit pengguna.
- Upload file dari aplikasi dengan file picker Windows, kategori tujuan, nama tampilan, tipe, dan opsi wajib/opsional untuk checklist.
- File Word `.docx` didaftarkan untuk editor Surat Generator. Untuk `.doc`, dukung pembukaan eksternal atau alur konversi ke `.docx` dengan persetujuan pengguna; jangan klaim parser placeholder `.doc` setara dengan `.docx`.
- File Excel `.xlsx/.xls`, PDF, gambar, dan file lain yang diizinkan disimpan dan dapat dibuka dengan aplikasi terkait. **Hanya Word yang dapat diedit melalui form internal.** Excel disalin apa adanya dari master; jangan memodifikasi workbook secara otomatis.
- Upload menggandakan file ke penyimpanan master yang dikelola aplikasi; tidak hanya menyimpan path sumber sementara.
- Saat mengganti file master, simpan versi lama sebagai cadangan, perbarui metadata versi, dan gunakan versi baru **hanya untuk pekerjaan berikutnya**. Dokumen pekerjaan lama tidak diubah.
- Jangan menghapus file yang masih direferensikan secara permanen tanpa pemeriksaan dan konfirmasi; pertimbangkan soft delete/archive.
- Gunakan nama fisik atau ID internal yang aman untuk mencegah collision; simpan nama tampilan asli dalam metadata.
- Tidak ada sinkronisasi Google Drive untuk Master Dokumen. Jika sudah ada kode terkait, jangan menghapusnya secara membabi buta; laporkan dan usulkan isolasi/penonaktifan fitur master-sync tanpa memengaruhi fungsi template surat yang lain.

## E. PROFIL PEKERJAAN DAN PREFILL WORD (MEKANISME B)

Form Buat Pekerjaan minimal meminta nama kapal dan nama pekerjaan. Sediakan field opsional seperti nomor kontrak, tanggal kontrak, nilai pekerjaan, perusahaan, penerima, lokasi pekerjaan, serta **custom fields** yang dapat ditambahkan.

Saat dokumen Word dibuka dari Detail Pekerjaan:
1. Baca placeholder dari **salinan template dasar** yang terkait dokumen pekerjaan (reuse engine yang sudah ada).
2. Cocokkan placeholder dengan field profil pekerjaan melalui mapping/alias yang disimpan (contoh `{kapal}` dan `{nama kapal}`).
3. Isi nilai yang ditemukan ke form (prefill), tanpa mengunci field tersebut.
4. Pengguna dapat mengubah nilai khusus dokumen atau mengisi field yang belum tersedia.
5. Live preview mengikuti perilaku aplikasi yang sudah ada.
6. Simpan hasil Word ke folder pekerjaan, beserta **nilai field per dokumen** dan referensi template dasar agar dapat diedit ulang. Jangan menghilangkan kemampuan re-edit hanya karena placeholder telah diganti pada output DOCX.
7. **Perubahan manual pada satu dokumen tidak otomatis mengubah profil pekerjaan atau dokumen lain.** Jika pengguna ingin menyimpan nilai sebagai field bersama, sediakan tindakan eksplisit dengan konfirmasi.
8. Ketika profil pekerjaan berubah setelah dokumen pernah diedit, jangan menimpa nilai manual; tampilkan opsi perbarui field tertentu secara sadar.

Gunakan engine Word dan preview yang sudah ada. Pertahankan formatting Word (tabel, header/footer, run, dan sebagainya) sejauh engine existing mendukungnya. Jangan membuat engine dokumen kedua.

## F. DETAIL PEKERJAAN DAN CHECKLIST KELENGKAPAN

Halaman Detail Pekerjaan menampilkan:
- Ringkasan nama kapal, pekerjaan, profil, tanggal pembuatan, status, dan path.
- Daftar kategori sesuai **snapshot struktur pekerjaan tersebut**, bukan struktur master terbaru.
- Daftar file tiap kategori, format, asal (salinan master/upload manual), tanggal, status, dan tindakan.
- Tombol **Isi Dokumen** untuk Word, **Buka** untuk file eksternal, **Upload File** per kategori, **Buka Folder Windows**, dan **Refresh Dokumen**.
- Ringkasan checklist: jumlah wajib, belum tersedia, belum lengkap, dalam proses, selesai; progres berdasarkan **dokumen wajib**, bukan semata jumlah file yang sudah ada.

Status dokumen: `Belum Tersedia`, `Belum Lengkap`, `Dalam Proses`, `Selesai`.
- File master yang sudah disalin **tidak otomatis Selesai**.
- Word dapat diperiksa untuk placeholder wajib yang belum diisi, tetapi status final tetap mengikuti aturan validasi/konfirmasi yang disepakati.
- PDF/Excel/gambar dapat ditandai Selesai secara manual setelah diperiksa.
- Dokumen manual yang belum diunggah ditampilkan sebagai kebutuhan checklist bila didaftarkan sebagai item wajib dalam kategori (contoh Efaktur, SPMK, JAMLAK, JAMHAR, Pengumuman Tender, Penunjukan Pemenang, Format Verifikasi).
- Jika file disalin atau dihapus langsung melalui Explorer, tombol Refresh melakukan rekonsiliasi metadata dengan filesystem dan tidak menghapus catatan penting tanpa konfirmasi.

## G. UPLOAD PER KATEGORI DAN BUKA FOLDER WINDOWS

- Upload multi-file melalui dialog Windows ke subfolder kategori yang tepat.
- Validasi format yang diizinkan, ukuran jika dikonfigurasi, keberadaan file, nama/path, akses folder, dan ruang disk bila memungkinkan.
- Jika nama file sama, tampilkan pilihan **Ganti dengan backup**, **Simpan sebagai nama lain**, atau **Batal**; jangan overwrite diam-diam.
- Buka folder pekerjaan atau kategori dengan File Explorer menggunakan API Windows yang aman; pastikan path berasal dari data aplikasi yang valid, bukan perintah shell bebas.
- Pengguna tetap dapat menambahkan dokumen melalui Explorer, lalu Refresh di aplikasi.

## H. PROTEKSI DUPLIKASI DAN KEAMANAN FILE

- Cek duplikasi pada database **dan** filesystem (case-insensitive sesuai Windows) sebelum membuat pekerjaan.
- Jangan pernah menimpa folder pekerjaan yang sudah ada secara otomatis. Tawarkan **Buka pekerjaan lama**, **Buat dengan nama berbeda**, atau **Batal**.
- Sanitasi nama kapal/pekerjaan untuk path; simpan nilai aslinya di profil. Tangani reserved names Windows, nama sangat panjang, collision, dan path traversal.
- Proses generate harus aman dari hasil parsial: buat di direktori staging pada volume tujuan, salin dan verifikasi file, kemudian finalisasi secara atomik sejauh memungkinkan. Bila gagal, bersihkan staging yang dibuat sendiri, jangan menyentuh file pengguna yang sudah ada.
- Pastikan perubahan database dan filesystem terkoordinasi; jika terjadi kegagalan, laporkan status yang dapat dipulihkan. Gunakan logging yang tidak mengekspos isi dokumen sensitif.
- Gunakan stream/asynchronous I/O agar UI tidak freeze; tampilkan progress dan pesan error yang jelas.

## I. BACKUP DAN RESTORE LOKAL

- Backup manual dan opsi backup otomatis (misalnya harian saat aplikasi dijalankan), dengan lokasi tujuan yang dapat dipilih.
- Backup mencakup: database SQLite (gunakan metode backup konsisten), konfigurasi yang relevan, Master Dokumen beserta versinya, serta folder pekerjaan yang dipilih/semua sesuai opsi.
- Sertakan manifest: versi format backup, waktu, daftar file, ukuran, hash, serta metadata untuk restore.
- Verifikasi integritas backup; jangan menganggap backup berhasil hanya karena file ZIP dibuat.
- Restore menyediakan preview isi, pilihan cakupan, peringatan konflik, dan konfirmasi eksplisit. **Jangan menimpa data aktif tanpa backup pra-restore dan persetujuan pengguna.**
- Backup disimpan di luar folder sumber yang sedang dibackup agar tidak rekursif; sediakan rekomendasi drive terpisah.
- Jika memakai ZIP, lindungi terhadap path traversal (zip-slip), file berukuran berlebihan, dan arsip rusak saat restore.
- **Tidak ada kewajiban cloud** untuk backup atau Master Dokumen.

## J. DESAIN DATA (SESUAIKAN DENGAN DATABASE EXISTING)

Entitas konseptual (nama final mengikuti konvensi repository):
- `Projects`: ID, nama kapal asli, nama pekerjaan asli, path, waktu, status, versi struktur.
- `ProjectFields`: project ID, key field, value, type, sumber.
- `DocumentCategories`: ID stabil, nama, urutan, aktif/nonaktif, versi.
- `MasterDocuments`: ID, category ID, nama, path managed, tipe, wajib/opsional, aktif, current version.
- `MasterDocumentVersions`: master document ID, version, path, hash, timestamp.
- `ProjectCategories`: snapshot ID kategori, urutan, nama folder/path untuk masing-masing proyek.
- `ProjectDocuments`: project ID, category snapshot ID, master reference (nullable), file path, status, source, version.
- `DocumentFieldValues`: project document ID, placeholder key, value, override flag.
- `PlaceholderMappings`: alias placeholder ke field profil.
- `BackupRecords`: waktu, lokasi, tipe, hasil verifikasi.

**Jangan membuat tabel baru bila konsep yang sama sudah ada.** Tambahkan migrasi aman dan uji kompatibilitas data lama.

## K. UI/UX WAJIB KONSISTEN DENGAN APLIKASI EXISTING

- Tambahkan navigasi `Manajemen Pekerjaan` dan `Master Dokumen` melalui sistem navigasi yang ada; hindari sidebar baru atau jendela terpisah yang tidak konsisten.
- Reuse theme, control, card, dialog, icon, resource dictionary, state/loading, animasi, dan pola layout existing.
- Halaman yang diperlukan: daftar pekerjaan + pencarian, form pekerjaan baru, detail pekerjaan, Master Struktur Folder, Master File, dan pengaturan lokasi/backup.
- Desain tetap rapi pada resolusi Windows desktop yang wajar; gunakan scroll untuk daftar panjang; hindari clipping, overflow, dan tombol tidak dapat dijangkau.
- Jangan mengubah editor Word dua-panel yang sudah ada; integrasikan entry point dari Detail Pekerjaan dan prefill profil.
- Tambahkan empty states, loading/progress, error states, dan konfirmasi aksi destruktif.
- Hindari animasi berlebihan yang mengganggu operasi file; ikuti pola animasi existing.

## L. FASE PENGERJAAN — WAJIB SATU PER SATU

### FASE 0 — AUDIT REPOSITORY DAN RENCANA INTEGRASI (BELUM IMPLEMENTASI)
1. Identifikasi solution, proyek, framework, pola MVVM, struktur navigasi, theme/UI, layanan file, template/Word, database, test, dan konfigurasi.
2. Catat fitur yang sudah ada, yang belum ada, dan titik integrasi paling aman.
3. Susun daftar file yang perlu ditambah/diubah, rencana migrasi, risiko regresi, dan langkah pengujian.
4. Tunjukkan apakah ada Google Drive untuk Template Surat dan/atau Master Dokumen. Jangan menghapus atau mengubahnya dalam fase audit.
5. **Jangan menulis kode pada fase ini kecuali pengguna secara eksplisit menyetujui.** Laporkan rencana dan **STOP**.

### FASE 1 — MODEL DATA, MIGRASI AMAN, DAN LAYANAN DASAR
Implementasikan model/metadata yang diperlukan dengan reuse schema existing; konfigurasi path lokal; validasi path; tes migrasi dan operasi file. Build/test. **STOP**.

### FASE 2 — MASTER STRUKTUR FOLDER FLEKSIBEL
Implementasikan kategori default sebagai seed, tambah/edit/urut/aktif/nonaktif, validasi, versi/snapshot, UI konsisten. Build/test. **STOP**.

### FASE 3 — MASTER DOKUMEN LOKAL
Implementasikan upload file master, penggantian dengan versi, hapus/arsip aman, pengelompokan kategori, lokasi penyimpanan, dan tampilan daftar file. **Tanpa sinkronisasi Master Google Drive.** Build/test. **STOP**.

### FASE 4 — BUAT PEKERJAAN DAN GENERATE FOLDER
Implementasikan profil pekerjaan, lokasi output, cek duplikasi, generate folder berdasarkan snapshot struktur, salin semua file master aktif otomatis, staging/rollback, progress, dan pencatatan metadata. Build/test termasuk simulasi kegagalan. **STOP**.

### FASE 5 — DETAIL PEKERJAAN, UPLOAD, DAN EXPLORER
Implementasikan detail pekerjaan, daftar folder/file, upload per kategori, buka File Explorer, refresh filesystem, dan penanganan collision. Build/test. **STOP**.

### FASE 6 — PREFILL WORD DAN EDITOR EXISTING
Integrasikan profil pekerjaan ke placeholder Word melalui engine existing; mapping alias; manual override; nilai per dokumen; re-edit; simpan output ke folder pekerjaan tanpa mengubah master. Build/test regresi preview/export. **STOP**.

### FASE 7 — CHECKLIST KELENGKAPAN
Implementasikan daftar kebutuhan wajib, status dokumen, validasi field Word, penandaan manual file non-Word, ringkasan progres, dan filter. Build/test. **STOP**.

### FASE 8 — BACKUP, RESTORE, DAN PENGUJIAN MENYELURUH
Implementasikan backup/restore aman, integritas, penanganan konflik, uji end-to-end, uji migrasi data lama, uji offline, dan regresi UI/fitur lama. Build/test. **STOP**.

### FASE 9 — HARDENING DAN DOKUMENTASI
Perbaiki masalah yang ditemukan, rapikan tanpa refactor destruktif, dokumentasikan penggunaan, lokasi data, format backup, serta prosedur pemulihan. Jangan mengganti packaging/installer existing tanpa izin. Build/test final. **STOP**.

## M. KRITERIA PENERIMAAN END-TO-END

1. Aplikasi existing masih bisa dijalankan dan fitur generator surat lama tetap bekerja.
2. Pengguna bisa mengubah urutan folder master, menambah kategori, dan menambah file master tanpa mengubah kode.
3. Saat membuat `KRI TBT - 516 / Perbaikan Sistem Kelistrikan`, folder utama dan semua kategori aktif terbentuk sesuai urutan saat generate.
4. Semua file master yang benar-benar tersedia disalin ke kategori tepat; kategori tanpa master tetap ada.
5. File master tidak berubah ketika salinan proyek diedit.
6. Membuka Word dari pekerjaan menampilkan prefill profil dan membolehkan perubahan manual khusus dokumen.
7. File Word hasil edit bisa dibuka ulang untuk mengedit field; preview dan export lama tidak rusak.
8. PDF/Excel/gambar dapat diunggah ke kategori dan dibuka dengan cara yang sesuai, tetapi tidak diedit dalam form internal.
9. Checklist tidak menganggap salinan template otomatis selesai.
10. Folder duplikat tidak tertimpa; kegagalan penyalinan tidak meninggalkan proyek yang salah ditandai berhasil.
11. Perubahan master berikutnya tidak mengubah proyek lama.
12. Backup dapat diverifikasi dan restore diuji pada data uji, tanpa menimpa data aktif diam-diam.
13. Seluruh fungsi utama bekerja offline.
14. Tampilan dan navigasi konsisten dengan UI aplikasi sebelum pengembangan.
15. Tidak ada fitur sinkronisasi Master Dokumen ke Google Drive.

## N. FORMAT LAPORAN SETIAP FASE

Tulis laporan ringkas dengan urutan:
1. **Fase yang dikerjakan** dan tujuan.
2. **Temuan existing** yang relevan serta keputusan integrasi.
3. **File ditambah/diubah** (path nyata dari repository).
4. **Perubahan database/migrasi** dan keamanan data lama.
5. **Hasil build dan test aktual** (perintah, pass/fail, error jika ada).
6. **Pemeriksaan regresi UI dan fitur lama**.
7. **Risiko atau keputusan yang perlu persetujuan**.
8. **Instruksi untuk pengguna** jika ada langkah manual.
9. Tutup dengan: **`FASE SELESAI — MENUNGGU "LANJUT PHASE BERIKUTNYA"`**.

## O. PERINTAH MULAI

**MULAI DARI FASE 0 SAJA.** Audit repository yang sedang terbuka di OpenCode. Jangan membuat proyek baru, jangan langsung mengimplementasikan modul, jangan mengganti desain UI, dan jangan mengubah fitur yang sudah ada. Setelah audit, ajukan rencana integrasi yang aman dan tunggu persetujuan pengguna.
