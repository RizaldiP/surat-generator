using System.ComponentModel;
using System.Reflection;
using SuratGenerator.Core.Models;

namespace SuratGenerator.Tests;

public class ProjectDetailProgressTests
{
    [Fact]
    public async Task RunAsyncMenampilkanSeratusPersenSebelumProgressDisembunyikan()
    {
        var type = typeof(SuratGenerator.Desktop.App).Assembly
            .GetType("SuratGenerator.Desktop.ViewModels.ProjectDetailViewModel", throwOnError: true)!;
        var viewModel = Activator.CreateInstance(type, new object?[] { null, null, null, null, null, null, null })!;
        var percentages = new List<int>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "ProgressPercent")
            {
                percentages.Add((int)GetProperty(type, viewModel, "ProgressPercent"));
            }
        };

        var method = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(viewModel, new object[] { (Func<Task>)(() => Task.CompletedTask) })!;
        await task;

        Assert.Contains(100, percentages);
        Assert.Equal(0, GetProperty(type, viewModel, "ProgressPercent"));
        Assert.False((bool)GetProperty(type, viewModel, "IsBusy"));
    }

    [Fact]
    public async Task ProgressLamaDiabaikanSetelahRunAsyncSelesai()
    {
        var type = typeof(SuratGenerator.Desktop.App).Assembly
            .GetType("SuratGenerator.Desktop.ViewModels.ProjectDetailViewModel", throwOnError: true)!;
        var viewModel = Activator.CreateInstance(type, new object?[] { null, null, null, null, null, null, null })!;
        var percentages = new List<int>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "ProgressPercent")
            {
                percentages.Add((int)GetProperty(type, viewModel, "ProgressPercent"));
            }
        };

        var runAsync = type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)runAsync.Invoke(viewModel, new object[] { (Func<Task>)(() => Task.CompletedTask) })!;
        percentages.Clear();

        var createProgress = type.GetMethod("CreateProgress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var progress = (IProgress<ProjectCreationProgressInfo>)createProgress.Invoke(viewModel, null)!;
        progress.Report(new ProjectCreationProgressInfo("Master", 95, "Lama"));
        await Task.Delay(50);

        Assert.DoesNotContain(95, percentages);
    }

    private static object GetProperty(Type type, object instance, string name) =>
        type.GetProperty(name)!.GetValue(instance)!;
}
