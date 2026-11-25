using System.Windows;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Desktop.Services;

internal sealed class ConfirmationService : IConfirmationService
{
    public bool ConfirmReset()
    {
        return MessageBox.Show(
            "Reset semua data surat yang sedang diisi?",
            "Konfirmasi Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteTemplate(string templateName)
    {
        return MessageBox.Show(
            $"Hapus template '{templateName}'? Draft yang terkait juga akan dihapus, sedangkan riwayat surat tetap disimpan.",
            "Konfirmasi Hapus Template",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeactivateCategory(string categoryName)
    {
        return MessageBox.Show(
            $"Nonaktifkan kategori '{categoryName}'? Kategori tidak akan dipakai untuk pekerjaan baru, tetapi struktur pekerjaan lama tetap utuh.",
            "Konfirmasi Nonaktifkan Kategori",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteCategory(string categoryName)
    {
        return MessageBox.Show(
            $"Hapus kategori '{categoryName}' dari Master Struktur Folder? Item wajib kategori ini juga dihapus. Proyek dan master dokumen yang sudah ada tetap tersimpan; perubahan hanya berlaku untuk pekerjaan baru.",
            "Konfirmasi Hapus Kategori",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmReplaceMaster(string displayName)
    {
        return MessageBox.Show(
            $"Ganti file master '{displayName}' dengan versi baru? File lama tetap tersimpan sebagai riwayat versi.",
            "Konfirmasi Ganti File Master",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeactivateMaster(string displayName)
    {
        return MessageBox.Show(
            $"Nonaktifkan master '{displayName}'? Master tidak akan dipakai untuk pekerjaan baru, tetapi file dan versi lama tetap tersimpan.",
            "Konfirmasi Nonaktifkan Master",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmArchiveMaster(string displayName)
    {
        return MessageBox.Show(
            $"Arsipkan master '{displayName}'? Master akan dinonaktifkan dan tidak dihapus dari penyimpanan.",
            "Konfirmasi Arsipkan Master",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmRelocateMaster(string oldPath, string newPath)
    {
        return MessageBox.Show(
            $"Pindahkan lokasi master dokumen?\n\nDari: {oldPath}\nKe: {newPath}\n\nSemua file master akan dipindahkan dan pengaturan diperbarui.",
            "Konfirmasi Pindah Lokasi Master",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmPromoteField(string key)
    {
        return MessageBox.Show(
            $"Simpan nilai field '{key}' ke profil pekerjaan? Nilai ini akan dipakai sebagai prefill oleh dokumen lain. Nilai manual pada dokumen lain tidak berubah.",
            "Konfirmasi Simpan ke Profil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmRefreshProfileFields()
    {
        return MessageBox.Show(
            "Perbarui field yang belum diubah manual dari profil pekerjaan? Nilai yang sudah Anda ubah akan tetap dipertahankan.",
            "Konfirmasi Perbarui dari Profil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmReplaceOutput(string path)
    {
        return MessageBox.Show(
            $"File hasil edit sudah ada:\n\n{path}\n\nFile lama akan disimpan sebagai backup sebelum ditimpa. Lanjutkan?",
            "Konfirmasi Simpan Hasil Edit",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteChecklistItem(string displayName)
    {
        return MessageBox.Show(
            $"Hapus item wajib '{displayName}' dari Master Struktur? Pekerjaan yang sudah dibuat tidak berubah.",
            "Konfirmasi Hapus Item Wajib",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmToggleChecklistItem(string displayName, bool nextActive)
    {
        var action = nextActive ? "Aktifkan" : "Nonaktifkan";
        return MessageBox.Show(
            $"{action} item wajib '{displayName}'? Pekerjaan yang sudah dibuat tidak berubah.",
            $"Konfirmasi {action} Item Wajib",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmMarkComplete(string displayName)
    {
        return MessageBox.Show(
            $"Tandai dokumen '{displayName}' sebagai Selesai? Pastikan semua field wajib sudah terisi.",
            "Konfirmasi Tandai Selesai",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteProject(string shipName, string jobName)
    {
        return MessageBox.Show(
            $"Hapus pekerjaan '{shipName} - {jobName}' dari daftar aplikasi? Folder di File Explorer tidak dihapus dan catatan dokumen di dalamnya akan hilang dari aplikasi.",
            "Konfirmasi Hapus Pekerjaan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmDeleteDocument(string displayName, bool hasFile)
    {
        var detail = hasFile
            ? "File di File Explorer juga akan dihapus."
            : "Dokumen ini belum memiliki file; hanya catatannya yang dihapus.";
        return MessageBox.Show(
            $"Hapus dokumen '{displayName}' dari pekerjaan ini? {detail}",
            "Konfirmasi Hapus Dokumen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    public bool ConfirmRestore(BackupPreview preview)
    {
        var lines = new List<string>
        {
            "Restore akan disiapkan sekarang dan diselesaikan setelah aplikasi direstart.",
            $"Dibuat: {preview.Manifest.CreatedAt.LocalDateTime:dddd, dd MMMM yyyy HH:mm}",
            $"Jumlah file: {preview.Manifest.Entries.Count}"
        };

        if (preview.Conflicts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Data yang akan ditimpa:");
            lines.AddRange(preview.Conflicts.Select(item => $"- {item}"));
        }

        if (preview.Warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Perhatian:");
            lines.AddRange(preview.Warnings.Select(item => $"- {item}"));
        }

        return MessageBox.Show(
            string.Join(Environment.NewLine, lines),
            "Konfirmasi Restore Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
