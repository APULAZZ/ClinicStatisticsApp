using ClinicStatisticsApp.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public sealed class TaskNotificationsWindow : Window
{
    public TaskNotificationsWindow(IReadOnlyList<WorkTaskNotification> notifications)
    {
        Title = "Уведомления о задачах"; Width = 540; Height = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = notifications.Count == 0 ? "Новых уведомлений нет" : "Последние уведомления", FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = Brush("#172033"), Margin = new Thickness(0, 0, 0, 14) });
        var list = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        foreach (var notification in notifications)
        {
            var item = new StackPanel();
            item.Children.Add(new TextBlock { Text = notification.Message, TextWrapping = TextWrapping.Wrap, Foreground = Brush("#172033") });
            item.Children.Add(new TextBlock { Text = notification.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), Margin = new Thickness(0, 5, 0, 0), FontSize = 11, Foreground = Brush("#64748B") });
            list.Items.Add(new Border { Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 7), Background = notification.ReadAt is null ? Brush("#EFF6FF") : Brush("#F8FAFC"), CornerRadius = new CornerRadius(7), Child = item });
        }
        panel.Children.Add(list); Content = panel;
    }
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
}
