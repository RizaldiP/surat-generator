# Panduan Penggunaan

## Alur Singkat

1. Buka Surat Generator.
2. Buka Pengaturan, pilih folder yang berisi template `.docx` atau `.doc`, lalu simpan.
3. Buka Buat Surat, pilih template hasil pindai folder.
4. Isi field yang terdeteksi otomatis.
5. Periksa pratinjau, lalu simpan draft, ekspor, atau cetak.

Seluruh alur bekerja tanpa internet. Microsoft Word 2016+ tetap diperlukan untuk pratinjau PDF, ekspor Word, dan cetak.

## Menyiapkan Folder Template

1. Kumpulkan semua file template `.docx` atau `.doc` dalam satu folder di komputer. Folder ini boleh berasal dari Google Drive for Desktop, OneDrive, folder jaringan, atau folder lokal biasa selama aplikasi dapat membacanya.
2. Buka halaman **Pengaturan**.
3. Tekan **Pilih Folder**, lalu pilih folder tersebut.
4. Centang **Sertakan subfolder** jika template juga disimpan di dalam subfolder.
5. Tekan **Simpan**. Aplikasi langsung memindai folder, menampilkan ringkasan hasil, dan menyaring daftar template ke folder yang baru dipilih.
6. Perubahan file baru, file diubah, atau file dipindahkan terdeteksi otomatis dalam beberapa detik. Tombol **Pindai Sekarang** tetap tersedia bila ingin menyelaraskan secara manual.

Aplikasi menyalin template ke folder data aplikasi dan tidak mengubah file asli. Template dari folder/path lama tetap tersimpan di data aplikasi, tetapi disembunyikan dari daftar; ringkasan pemindaian memberi tahu jumlah template yang disembunyikan. File `.doc` dikonversi otomatis ke `.docx` sementara dengan Microsoft Word, lalu hasil konversi dibersihkan setelah template dibaca. File sementara Word yang diawali `~$` diabaikan.

## Buat Surat

### Memilih Template

- Gunakan kolom **Cari template...** untuk memfilter daftar berdasarkan nama, nama file, atau kategori.
- Filter tetap aktif setelah pemindaian otomatis maupun setelah menekan **Pindai Folder**; kosongkan kolom untuk menampilkan semua template.
- Centang **Favorit saja** untuk menampilkan hanya template yang sudah ditandai favorit. Template yang sedang dipakai tetap aktif walau bukan favorit.
- Gunakan daftar template di bagian atas halaman Buat Surat.
- Template berasal dari hasil pindai folder yang diatur di Pengaturan, termasuk subfolder bila opsi **Sertakan subfolder** aktif.
- Template dari folder/path lama tidak dihapus, tetapi tidak ditampilkan pada halaman Buat Surat maupun Template.
- Daftar diperbarui otomatis saat folder template berubah. Tekan **Pindai Folder** hanya bila ingin menyelaraskan secara manual tanpa berpindah halaman.
- Placeholder ditulis dengan format `{nama field}`, misalnya `{tanggal surat}` atau `{nomor surat}`.
- Placeholder dikenali dari paragraf, tabel, header, footer, dan teks yang terpecah menjadi beberapa Word run.
- Placeholder yang sama hanya membuat satu field dan nilainya dipakai bersamaan.

### Mengisi Form dan Pratinjau

- Field dibuat otomatis dari placeholder dengan label berbasis nama field.
- Pratinjau diperbarui sekitar 600 ms setelah pengisian berhenti.
- Scroll pada kartu **Data Surat** dan **Pratinjau** hanya menggulir isi kartu tersebut; judul, toolbar, dan kartu tombol di bawah tetap pada posisinya.
- Gunakan zoom in, zoom out, atau Fit Width untuk mengatur tampilan.
- Jika pratinjau gagal, periksa ketersediaan Microsoft Word dan validitas template, lalu gunakan tombol coba lagi.
- Hasil render lama diabaikan ketika ada permintaan pratinjau baru.

### Menyimpan dan Mengeluarkan Hasil

- Simpan Draft menyimpan draft saat ini; perubahan juga disimpan otomatis setelah jeda singkat.
- Reset membersihkan form setelah konfirmasi.
- Ekspor Word menghasilkan `.docx` melalui dialog Simpan.
- Ekspor PDF menghasilkan `.pdf` melalui dialog Simpan dan memerlukan Microsoft Word.
- Cetak mengirim dokumen ke printer melalui Microsoft Word.
- Setiap ekspor atau cetak dicatat pada halaman Riwayat Surat.

## Mengelola Template

Pada halaman Template Anda dapat:

- Mencari template secara real-time berdasarkan nama atau bagian kata.
- Memfilter berdasarkan kategori dan mengurutkan berdasarkan nama atau tanggal.
- Menandai favorit, mengubah nama, kategori, dan versi, atau menghapus salinan lokal dengan konfirmasi.
- Centang **Favorit saja** untuk menyaring daftar; melepaskan favorit saat filter aktif membuat baris tersebut langsung hilang.
- Menekan **Refresh** untuk memuat ulang daftar lokal.
- Menekan **Pindai Folder** untuk menyelaraskan daftar secara manual; pemindaian otomatis juga berjalan saat folder berubah.
- Melihat kolom **Sumber** untuk mengetahui apakah file asli masih ada di folder.

Perubahan nama, kategori, versi, dan favorit dipertahankan saat file di folder diperbarui. Jika file dihapus dari folder, salinan lokal tetap disimpan dan ditandai **Tidak ada di folder** agar draft dan riwayat tidak rusak. Jika file dikembalikan dengan nama baru dan isi sama, aplikasi memetakannya kembali ke template yang sudah ada.

## Master Struktur Folder

Pada halaman **Master Struktur Folder** Anda dapat:

- Menambah, mengedit, mengurutkan, mengaktifkan, menonaktifkan, dan menghapus kategori folder.
- Mengatur item wajib untuk kategori yang dipilih.
- Menghapus kategori beserta item wajibnya dengan konfirmasi.

Perubahan struktur hanya berlaku untuk pekerjaan baru atau selanjutnya. Pekerjaan yang sudah dibuat tetap memakai strukturnya sendiri. Master dokumen yang memakai kategori yang dihapus tetap tersimpan, tetapi kolom kategorinya ditampilkan sebagai `-`.

## Catatan

Pada halaman **Catatan** Anda dapat:

- Menambah, mengedit, mengurutkan, dan menghapus catatan, misalnya catatan **Pembuatan SIK**.
- Mengisi tiap syarat dengan nama dan keterangan, misalnya **Surat Permohonan**, **SC**, dan **SPMK**.
- Menambah, mengedit, mengurutkan, dan menghapus syarat pada catatan yang dipilih.
- Mencari catatan berdasarkan judul, nama syarat, atau keterangan. Pencarian juga menyaring syarat yang ditampilkan.
- Menekan **Refresh** untuk memuat ulang data.
- Tombol **Simpan** pada dialog catatan dan syarat juga bisa dipicu dengan **Ctrl+Enter**.

Catatan berdiri sendiri sebagai referensi. Mengubah atau menghapus catatan tidak memengaruhi Master Struktur Folder, master dokumen, atau pekerjaan yang sudah dibuat.

## Manajemen Pekerjaan

- Tombol **Buat Pekerjaan** membuat folder proyek dan menyalin master dokumen aktif dari kategori yang aktif. Master yang nonaktif atau diarsipkan tidak disalin dan tidak menampilkan peringatan pada detail pekerjaan.
- Jika file master aktif berubah di luar aplikasi sehingga checksum-nya tidak sinkron, aplikasi memperbarui checksum dari file aktual saat master dipakai dan tetap melanjutkan pembuatan atau refresh pekerjaan.
- Tombol **Refresh Dokumen** menyelaraskan folder dengan catatan aplikasi, termasuk file yang ditambahkan langsung dari File Explorer dan master aktif baru yang belum memiliki salinan di pekerjaan. Indikator progress tetap menampilkan 100% dengan keterangan **Selesai** sebelum disembunyikan.
- Jika sudah ada file dengan nama master yang sama di folder pekerjaan, file itu diadopsi sebagai salinan master tanpa ditimpa, walaupun isinya berbeda. Salinan master yang sama persis dan bertumpuk dibersihkan otomatis; salinan dengan isi berbeda dipertahankan dan diberi peringatan.
- Tombol **Hapus** pada baris dokumen meminta konfirmasi, lalu menghapus catatan dokumen beserta file-nya dari folder pekerjaan. Baris tanpa file hanya menghapus catatan aplikasi. Master yang dihapus dapat ditarik lagi dengan **Refresh Dokumen** tanpa membuat duplikat.
- Jika folder pekerjaan dihapus atau dipindahkan dari File Explorer, aplikasi menandai pekerjaan sebagai folder hilang dan menyembunyikannya dari daftar. Data aplikasi tetap tersimpan.
- Jika folder dikembalikan ke path asal, tekan **Periksa Ulang** pada banner detail. Pekerjaan akan muncul lagi di daftar.
- Tombol **Hapus dari Daftar** hanya menghapus catatan aplikasi untuk pekerjaan bersangkutan. Folder asli di File Explorer tidak dihapus.
- Halaman detail pekerjaan dapat digulir dengan mouse wheel untuk mencapai daftar dokumen dan **Field Profil** pada layar kecil.

## Menu Panduan

Halaman **Panduan** berisi ringkasan fungsi tiap menu, alur cepat penggunaan, ringkasan backup/restore, dan pemecahan masalah. Tombol **Buka** pada tiap baris menu membuka halaman yang bersangkutan.

## Draft dan Riwayat

- Halaman Draft menampilkan draft yang tersimpan. Pilih draft untuk membukanya kembali di halaman Buat Surat bersama nilai field sebelumnya.
- Halaman Riwayat Surat menampilkan catatan ekspor dan cetak. Untuk berkas ekspor, tersedia aksi membuka file atau membuka folder.
- Catatan cetak tidak memiliki file hasil dan hanya disimpan sebagai riwayat.

## Backup dan Restore

### Membuat Backup

1. Buka halaman **Pengaturan**, lalu bagian **Backup**.
2. Pada **Folder backup**, tekan **Pilih Folder Backup**, pilih folder tujuan, lalu tekan **Simpan Lokasi Backup**. Folder backup tidak boleh berada di dalam folder sumber dan tidak boleh mengandung folder sumber.
3. Centang **Semua proyek** atau pilih proyek tertentu.
4. Tekan **Buat Backup Sekarang**.
5. Arsip `.zip` dibuat di folder backup dan diverifikasi otomatis. Riwayat 10 backup terakhir ditampilkan di kartu Backup.

Backup harian otomatis dibuat saat aplikasi dibuka bila belum ada backup valid dalam 24 jam terakhir. Arsip yang sudah dihapus atau tidak valid tidak dihitung sebagai backup harian.

Arsip database mencakup seluruh data aplikasi, termasuk catatan beserta daftar syarat dan keterangannya. Saat memulihkan, centang **Database** agar data catatan ikut terambil.

### Memulihkan Backup

1. Buka **Pengaturan**, lalu bagian **Restore Backup**.
2. Tekan **Pilih Arsip**, lalu pilih arsip backup `.zip`.
3. Tekan **Periksa Arsip** sebelum menyiapkan restore. Tombol restore hanya aktif setelah pemeriksaan berhasil.
4. Periksa daftar file, konflik, komponen yang akan dipulihkan (database termasuk catatan, pengaturan, template, master dokumen), dan pilihan proyek.
5. Tekan **Siapkan Restore + Restart**, lalu konfirmasi.
6. Aplikasi membuat backup kondisi saat ini terlebih dahulu. Setelah itu, restart aplikasi untuk menyelesaikan restore.
7. Jika restore gagal saat restart, pesan kesalahan ditampilkan di Pengaturan dan file lama dikembalikan dari backup pra-restore.

Token Google Drive tidak disertakan dalam arsip. Lakukan login ulang setelah restore jika memakai fitur Drive.

## Pemecahan Masalah

| Gejala | Tindakan |
| --- | --- |
| Daftar template kosong | Pastikan folder sudah dipilih di Pengaturan, lalu tekan Pindai Sekarang. |
| Folder tidak ditemukan | Periksa apakah folder masih ada dan dapat diakses pengguna Windows saat ini. |
| Template tidak terbaca | Pastikan file berformat `.docx` atau `.doc`, tidak rusak, dan bukan file sementara Word. |
| Template `.doc` tidak terbaca | Pastikan Microsoft Word 2016+ terpasang dan dapat dijalankan untuk konversi. |
| File sedang dibuka Word tidak ikut terpindai | Tutup file tersebut, lalu pindai ulang. |
| Template hilang dari daftar | Periksa kolom Sumber. File yang dihapus dari folder tetap tersimpan sebagai salinan lokal. |
| Pekerjaan hilang dari daftar | Periksa path folder asal. Pekerjaan dengan folder hilang disembunyikan, bukan dihapus; kembalikan folder lalu tekan **Periksa Ulang** pada detail, atau hapus data aplikasinya bila memang tidak diperlukan. |
| Pratinjau, ekspor Word, atau cetak gagal | Pastikan Microsoft Word 2016+ terpasang dan dapat dijalankan. |
| Backup gagal dibuat | Periksa folder backup masih dapat ditulis, tidak berada di dalam folder sumber, dan tidak mengandung folder sumber. |
| Backup harian tidak muncul | Pastikan arsip terakhir masih ada di folder backup dan valid; arsip yang dihapus memicu backup baru saat aplikasi dibuka. |
| Tombol restore tidak aktif | Pilih arsip lalu tekan **Periksa Arsip** terlebih dahulu. |
| Arsip ditolak saat diperiksa | Pastikan arsip berasal dari aplikasi ini, belum dimanipulasi, dan menggunakan format backup yang didukung. |
| Restore sudah disiapkan | Restart aplikasi untuk menyelesaikannya; tombol restore dinonaktifkan sampai restart selesai. |
| Restore gagal saat restart | Periksa pesan di kartu Restore Backup dan log aplikasi. Kondisi sebelum restore dikembalikan otomatis dari backup pra-restore. |
| Google Drive tidak terhubung setelah restore | Token tidak ikut backup. Buka pengaturan Drive lalu lakukan login ulang. |
| Butuh detail kesalahan | Periksa `%LOCALAPPDATA%\SuratGenerator\Logs\surat-generator.log`. |
