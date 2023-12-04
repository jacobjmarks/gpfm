using CommunityToolkit.Maui.Storage;
using Gpfm.Core;

namespace Gpfm.Gui;

[QueryProperty(nameof(Step), nameof(Step))]
public partial class EditStepPage : ContentPage
{
    public const string Route = "steps/edit";

    private JobStep step = null!;
    public JobStep Step
    {
        get => step;
        set
        {
            step = value;
            OnPropertyChanged(nameof(Step));
        }
    }

    public EditStepPage()
    {
        InitializeComponent();
        BindingContext = this;
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

            Step.Source = folderPickerResult.Folder.Path;
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
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void Entry_Completed(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
