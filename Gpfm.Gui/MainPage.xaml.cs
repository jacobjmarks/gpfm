using Gpfm.Core;
using System.Collections.ObjectModel;

namespace Gpfm.Gui;

public partial class MainPage : ContentPage
{
    public ObservableCollection<JobStep> Steps { get; set; } = [
        new("First Step", false, "file://foobar/first"),
        new("Second Step", false, "file://foobar/second"),
        new("Third Step", false, "file://foobar/third"),
    ];

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private void EditStepButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not JobStep step)
            throw new InvalidOperationException();
    }

    private void RemoveStepButton_Clicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not JobStep step)
            throw new InvalidOperationException();

        Steps.Remove(step);
    }
}
