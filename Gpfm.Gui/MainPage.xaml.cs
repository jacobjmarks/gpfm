using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Gpfm.Core;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace Gpfm.Gui;

public partial class MainPage : ContentPage
{
    public static ObservableCollection<JobStep> Steps { get; set; } = [];

    private string? jobFilePath;
    public string? JobFilePath
    {
        get => jobFilePath;
        set
        {
            jobFilePath = value;
            OnPropertyChanged();
        }
    }

    private string? output;
    public string? Output
    {
        get => output;
        set
        {
            output = value;
            OnPropertyChanged();
        }
    }

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void OpenButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not VisualElement button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            var pickerResult = await FilePicker.Default.PickAsync();
            if (pickerResult == null)
                return;

            Job job;
            try
            {
                job = await Job.OpenAsync(pickerResult.FullPath);
            }
            catch (FileNotFoundException)
            {
                await DisplayAlert("Error", $"File not found: {pickerResult.FullPath}", "OK");
                return;
            }
            catch (JsonException)
            {

                await DisplayAlert("Error", "Job configuration file is malformed.", "OK");
                return;
            }
            catch
            {
                await DisplayAlert("Error", "An error has occurred.", "OK");
                return;
            }

            JobFilePath = pickerResult.FullPath;

            Steps.Clear();
            foreach (var step in job.Config.Steps)
                Steps.Add(step);
            Output = job.Config.Output;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not VisualElement button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            if (string.IsNullOrEmpty(JobFilePath))
                return;

            if (!File.Exists(JobFilePath))
            {
                SaveAsButton_Clicked(sender, e);
                return;
            }

            var job = new Job(new(Steps, Output ?? ""));
            var serialized = job.Serialize();
            await File.WriteAllTextAsync(JobFilePath, serialized);

            Toast.Make("Saved");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void SaveAsButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not VisualElement button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            if (string.IsNullOrEmpty(JobFilePath))
                return;

            var job = new Job(new(Steps, Output ?? ""));
            var serialized = job.Serialize();

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(serialized));
            var saveResult = await FileSaver.Default.SaveAsync(".gpfm", stream);
            if (!saveResult.IsSuccessful || string.IsNullOrEmpty(saveResult.FilePath))
            {
                var errorMessage = saveResult.Exception?.Message ?? "An error has occurred.";
                await DisplayAlert("Error", errorMessage, "OK");
                return;
            }

            JobFilePath = saveResult.FilePath;

            Toast.Make("Saved");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void AddStepButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(AddStepPage.Route);
    }

    private async void EditStepButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject button || button.BindingContext is not JobStep step)
            throw new InvalidOperationException();

        await Shell.Current.GoToAsync(EditStepPage.Route, [new(nameof(EditStepPage.Step), step)]);
    }

    private void RemoveStepButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not BindableObject button || button.BindingContext is not JobStep step)
            throw new InvalidOperationException();

        Steps.Remove(step);
    }

    private async void SetOutputButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not VisualElement button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            var pickerResult = await FolderPicker.Default.PickAsync();
            if (!pickerResult.IsSuccessful || pickerResult.Folder == null)
            {
                var errorMessage = pickerResult.Exception?.Message ?? "An error has occurred.";
                await DisplayAlert("Error", errorMessage, "OK");
                return;
            }

            Output = pickerResult.Folder.Path;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void MergeButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            throw new InvalidOperationException();

        if (string.IsNullOrWhiteSpace(Output))
        {
            await DisplayAlert("Error", "Invalid output directory", "OK");
            return;
        }

        button.IsEnabled = false;
        try
        {
            var job = new Job(new(Steps, Output));
            await job.RunAsync();
            await DisplayAlert("Done", "Successfully merged.", "OK");
        }
        catch (Exception exception)
        {
            await DisplayAlert("Error", exception.Message, "OK");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
