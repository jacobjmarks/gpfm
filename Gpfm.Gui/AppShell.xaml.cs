namespace Gpfm.Gui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(EditStepPage.Route, typeof(EditStepPage));
    }
}
