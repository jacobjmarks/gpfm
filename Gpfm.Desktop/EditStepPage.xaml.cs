using CommunityToolkit.Maui.Storage;
using Gpfm.Core;

namespace Gpfm.Desktop;

[QueryProperty(nameof(Step), nameof(Step))]
public partial class EditStepPage : ContentPage
{
    public const string Route = "steps/edit";

    private JobStep _step = null!;
    public JobStep Step
    {
        get => _step;
        set
        {
            _step = value;
            StepName = _step.Name;
            StepSource = _step.Source;
        }
    }

    private string _stepName = null!;
    public string StepName
    {
        get => _stepName;
        set
        {
            _stepName = value;
            OnPropertyChanged();
        }
    }

    private string _stepSource = null!;
    public string StepSource
    {
        get => _stepSource;
        set
        {
            _stepSource = value;
            OnPropertyChanged();
        }
    }

    public EditStepPage()
    {
        InitializeComponent();
    }

    private async void SetSourceButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not VisualElement button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            var folderPickerResult = await FolderPicker.Default.PickAsync();
            if (!folderPickerResult.IsSuccessful || folderPickerResult.Folder == null)
            {
                var errorMessage = folderPickerResult.Exception?.Message ?? "An error has occurred.";
                await DisplayAlert("Error", errorMessage, "OK");
                return;
            }

            StepSource = folderPickerResult.Folder.Path;
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void CancelButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void OkButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button button)
            throw new InvalidOperationException();

        button.IsEnabled = false;
        try
        {
            SaveChanges();
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void Entry_Completed(object sender, EventArgs e)
    {
        SaveChanges();
        await Shell.Current.GoToAsync("..");
    }

    private void SaveChanges()
    {
        Step.Name = StepName;
        Step.Source = StepSource;
    }
}
