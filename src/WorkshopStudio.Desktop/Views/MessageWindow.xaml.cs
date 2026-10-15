using System.Windows;

namespace WorkshopStudio.Desktop.Views;

public partial class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool confirmation, MessageBoxImage kind)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        MessageText.Text = message;
        AcceptButton.Content = confirmation ? "Ja" : "OK";
        CancelButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        // Enter must not accidentally accept a destructive confirmation.
        AcceptButton.IsDefault = !confirmation;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            DialogResult = false;
        };
        CancelButton.IsDefault = confirmation;
        Symbol.Data = (System.Windows.Media.Geometry)FindResource(kind == MessageBoxImage.Error
            ? "IconAlert" : confirmation ? "IconHelp" : "IconInfo");
        if (kind == MessageBoxImage.Error) Symbol.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "DangerBrush");
        Loaded += (_, _) => { if (confirmation) CancelButton.Focus(); else AcceptButton.Focus(); };
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
