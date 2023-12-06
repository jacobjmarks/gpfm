using CommunityToolkit.Maui.Storage;
using Gpfm.Core;
using System.Collections.ObjectModel;

namespace Gpfm.Gui;

public partial class MainPage : ContentPage
{
    public static ObservableCollection<JobStep> Steps { get; set; } = [
        new("First Step", "file://foobar/first"),
        new("Second Step", "file://foobar/second"),
        new("Third Step", "file://foobar/third"),
    ];

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
            var jobConfig = new JobConfig(Steps, Output);
            var job = new Job(jobConfig);
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
