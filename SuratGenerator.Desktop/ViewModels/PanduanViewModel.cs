using CommunityToolkit.Mvvm.Input;
using SuratGenerator.Desktop.Services;
using SuratGenerator.Desktop.Views.Pages;

namespace SuratGenerator.Desktop.ViewModels;

internal sealed class PanduanViewModel
{
    private readonly INavigationService _navigation;

    public PanduanViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        OpenPageCommand = new RelayCommand<PanduanMenuRow>(row =>
        {
            if (row is not null)
            {
                row.Open();
            }
        });
        MenuItems = new[]
        {
            new PanduanMenuRow(
                "Buat Surat",
                "Pilih template, isi field otomatis, lihat pratinjau, lalu simpan draft, ekspor, atau cetak surat.",
                () => _navigation.NavigateTo<BuatSuratPage>()),
            new PanduanMenuRow(
                "Template",
                "Kelola salinan template: cari, filter, favorit, ubah nama/kategori/versi, dan pindai folder sumber.",
                () => _navigation.NavigateTo<TemplatePage>()),
            new PanduanMenuRow(
                "Master Struktur Folder",
                "Atur kategori dan item wajib untuk struktur pekerjaan baru. Pekerjaan lama tidak berubah.",
                () => _navigation.NavigateTo<MasterStructurePage>()),
            new PanduanMenuRow(
                "Master Dokumen",
                "Unggah, ganti, aktifkan/nonaktifkan, dan arsipkan dokumen master beserta riwayat versinya.",
                () => _navigation.NavigateTo<MasterDocumentPage>()),
            new PanduanMenuRow(
                "Manajemen Pekerjaan",
                "Buat pekerjaan baru, buka detailnya, kelola dokumen, dan generate struktur folder hasil.",
                () => _navigation.NavigateTo<ProjectListPage>()),
            new PanduanMenuRow(
                "Riwayat Surat",
                "Lihat catatan ekspor dan cetak surat, lalu buka file hasil atau foldernya.",
                () => _navigation.NavigateTo<RiwayatSuratPage>()),
            new PanduanMenuRow(
                "Draft",
                "Buka kembali draft yang tersimpan otomatis dari halaman Buat Surat.",
                () => _navigation.NavigateTo<DraftPage>()),
            new PanduanMenuRow(
                "Pengaturan",
                "Atur folder template, master dokumen, hasil pekerjaan, lokasi backup, serta backup dan restore.",
                () => _navigation.NavigateTo<PengaturanPage>()),
            new PanduanMenuRow(
                "Panduan",
                "Halaman ini: ringkasan fungsi menu, alur cepat, backup/restore, dan pemecahan masalah.",
                () => _navigation.NavigateTo<PanduanPage>())
        };
    }

    public IReadOnlyList<PanduanMenuRow> MenuItems { get; }

    public IRelayCommand<PanduanMenuRow> OpenPageCommand { get; }
}

internal sealed class PanduanMenuRow
{
    private readonly Action _open;

    public PanduanMenuRow(string name, string description, Action open)
    {
        Name = name;
        Description = description;
        _open = open;
    }

    public string Name { get; }

    public string Description { get; }

    public void Open() => _open();
}
