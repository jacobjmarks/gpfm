using CommunityToolkit.Maui.Storage;

namespace Gpfm.Desktop;

public partial class AddStepPage : ContentPage
{
    public const string Route = "steps/add";

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

    public AddStepPage()
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
            if (await SaveChangesAsync())
                await Shell.Current.GoToAsync("..");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void Entry_Completed(object sender, EventArgs e)
    {
        if (await SaveChangesAsync())
            await Shell.Current.GoToAsync("..");
    }

    private async Task<bool> SaveChangesAsync()
    {
        if (string.IsNullOrWhiteSpace(StepName))
        {
            await DisplayAlert("Error", "Invalid step name.", "OK");
            return false;
        }

        if (string.IsNullOrWhiteSpace(StepSource))
        {
            await DisplayAlert("Error", "Invalid step source.", "OK");
            return false;
        }

        MainPage.Steps.Add(new(StepName, StepSource));
        return true;
    }
}
