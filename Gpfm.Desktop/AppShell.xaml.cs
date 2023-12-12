namespace Gpfm.Desktop;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(AddStepPage.Route, typeof(AddStepPage));
        Routing.RegisterRoute(EditStepPage.Route, typeof(EditStepPage));
    }
}
