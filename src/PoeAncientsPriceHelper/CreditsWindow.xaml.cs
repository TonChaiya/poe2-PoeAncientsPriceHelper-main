using System.Windows;
using System.Windows.Controls;

namespace PoeAncientsPriceHelper;

// Small modal "thank you" dialog listing the community contributors. Opened from the Credits link
// in MainWindow. The list is static and curated (not the GitHub contributors graph) so PR authors
// whose work was squashed/reimplemented under the maintainer's commits are still credited.
public partial class CreditsWindow : Window
{
    private record Contributor(string Handle, string ProfileUrl);

    private static readonly Contributor[] Contributors =
    [
        new("dhaern",     "https://github.com/dhaern"),
        new("exploitz86", "https://github.com/exploitz86"),
        new("atomsbaza",  "https://github.com/atomsbaza"),
    ];

    public CreditsWindow()
    {
        InitializeComponent();
        foreach (var c in Contributors)
        {
            ContributorList.Children.Add(new TextBlock
            {
                Text = c.Handle,
                Tag = c.ProfileUrl,
                Style = (Style)Resources["LinkText"],
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
        // Wire the handle links here (rather than per-element XAML) so adding a contributor stays
        // a one-line edit to the array above.
        foreach (TextBlock link in ContributorList.Children)
            link.MouseLeftButtonUp += Link_Click;
    }

    // Esc closes the dialog, matching the ✕ — no extra confirmation for a read-only credits screen.
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) Close();
        base.OnKeyDown(e);
    }

    // Open a contributor/maintainer GitHub profile in the default browser. Same UseShellExecute
    // pattern as the update/donate links in MainWindow.
    private void Link_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string url } || string.IsNullOrEmpty(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Credits] failed to open browser: {ex.Message}");
        }
    }
}
