using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClinicStatisticsApp.UI.Views;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(string title, byte[] imageBytes)
    {
        InitializeComponent();
        Title = title;
        using var stream = new MemoryStream(imageBytes);
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        PreviewImage.Source = image;
    }
}
