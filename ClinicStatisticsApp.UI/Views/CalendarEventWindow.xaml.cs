using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows;

namespace ClinicStatisticsApp.UI.Views;

public partial class CalendarEventWindow : Window
{
    private readonly CalendarEvent _calendarEvent;
    private readonly bool _canEdit;
    private List<UserChoice> _allUsers = [];
    private readonly HashSet<int> _selectedParticipantIds = [];
    private bool _renderingParticipants;
    public CalendarEvent Event => _calendarEvent;
    public IReadOnlyCollection<int> ParticipantIds
    {
        get { CaptureParticipantSelection(); return _selectedParticipantIds.ToList(); }
    }
    public bool DeleteRequested { get; private set; }

    public CalendarEventWindow(CalendarEvent calendarEvent, int currentUserId, bool canManageAll)
    {
        InitializeComponent();
        _calendarEvent = calendarEvent;
        _canEdit = calendarEvent.Id == 0 || calendarEvent.CreatedByUserId == currentUserId || canManageAll;
        TitleTextBox.Text = calendarEvent.Title;
        DescriptionTextBox.Text = calendarEvent.Description ?? string.Empty;
        StartDatePicker.SelectedDate = calendarEvent.StartsAt.Date;
        EndDatePicker.SelectedDate = calendarEvent.EndsAt.Date;
        foreach (var time in Enumerable.Range(0, 48).Select(x => TimeSpan.FromMinutes(x * 30)))
        {
            StartTimeComboBox.Items.Add(time); EndTimeComboBox.Items.Add(time);
        }
        StartTimeComboBox.SelectedItem = new TimeSpan(calendarEvent.StartsAt.Hour, calendarEvent.StartsAt.Minute / 30 * 30, 0);
        EndTimeComboBox.SelectedItem = new TimeSpan(calendarEvent.EndsAt.Hour, calendarEvent.EndsAt.Minute / 30 * 30, 0);
        ColorComboBox.SelectedValue = calendarEvent.Color;
        RecurrenceComboBox.SelectedValue = calendarEvent.RecurrenceType;
        ReminderComboBox.SelectedValue = calendarEvent.ReminderMinutes?.ToString();
        AllDayCheckBox.IsChecked = calendarEvent.IsAllDay;
        LoadUsers();
        DeleteButton.Visibility = calendarEvent.Id != 0 && _canEdit ? Visibility.Visible : Visibility.Collapsed;
        SetEditable(_canEdit);
    }

    private void LoadUsers()
    {
        using var db = DbContextFactory.Create();
        _selectedParticipantIds.UnionWith(_calendarEvent.Participants.Select(x => x.UserId));
        _allUsers = db.Users.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new UserChoice(x.Id, x.FullName)).ToList();
        ParticipantsListBox.SelectionChanged += (_, _) => { if (!_renderingParticipants) CaptureParticipantSelection(); };
        RenderParticipants();
    }

    private void ParticipantsSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) { if (IsLoaded) { CaptureParticipantSelection(); RenderParticipants(); } }
    private void CaptureParticipantSelection() { foreach (var item in ParticipantsListBox.SelectedItems.OfType<UserChoice>()) _selectedParticipantIds.Add(item.Id); }
    private void RenderParticipants()
    {
        _renderingParticipants = true; ParticipantsListBox.Items.Clear(); var filter = ParticipantsSearchTextBox.Text.Trim();
        foreach (var user in _allUsers.Where(x => string.IsNullOrWhiteSpace(filter) || x.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        { ParticipantsListBox.Items.Add(user); if (_selectedParticipantIds.Contains(user.Id)) ParticipantsListBox.SelectedItems.Add(user); }
        _renderingParticipants = false;
    }

    private void SetEditable(bool editable)
    {
        TitleTextBox.IsReadOnly = !editable; DescriptionTextBox.IsReadOnly = !editable; StartDatePicker.IsEnabled = editable; EndDatePicker.IsEnabled = editable;
        StartTimeComboBox.IsEnabled = editable; EndTimeComboBox.IsEnabled = editable; ColorComboBox.IsEnabled = editable; AllDayCheckBox.IsEnabled = editable; RecurrenceComboBox.IsEnabled = editable; ReminderComboBox.IsEnabled = editable; ParticipantsListBox.IsEnabled = editable; ParticipantsSearchTextBox.IsEnabled = editable;
    }
    private void AllDayChanged(object sender, RoutedEventArgs e) { var enabled = AllDayCheckBox.IsChecked != true && _canEdit; StartTimeComboBox.IsEnabled = enabled; EndTimeComboBox.IsEnabled = enabled; }
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_canEdit) { DialogResult = false; return; }
        var startDate = StartDatePicker.SelectedDate ?? DateTime.Today; var endDate = EndDatePicker.SelectedDate ?? startDate;
        var startTime = AllDayCheckBox.IsChecked == true ? TimeSpan.Zero : (TimeSpan?)StartTimeComboBox.SelectedItem ?? TimeSpan.Zero;
        var endTime = AllDayCheckBox.IsChecked == true ? TimeSpan.FromDays(1) : (TimeSpan?)EndTimeComboBox.SelectedItem ?? TimeSpan.FromHours(1);
        _calendarEvent.Title = TitleTextBox.Text.Trim(); _calendarEvent.Description = DescriptionTextBox.Text.Trim(); _calendarEvent.StartsAt = startDate.Date + startTime; _calendarEvent.EndsAt = endDate.Date + endTime;
        _calendarEvent.IsAllDay = AllDayCheckBox.IsChecked == true; _calendarEvent.Color = ColorComboBox.SelectedValue?.ToString() ?? "#2563EB";
        _calendarEvent.RecurrenceType = RecurrenceComboBox.SelectedValue?.ToString() ?? "None";
        _calendarEvent.ReminderMinutes = int.TryParse(ReminderComboBox.SelectedValue?.ToString(), out var minutes) ? minutes : null;
        DialogResult = true;
    }
    private void DeleteButton_Click(object sender, RoutedEventArgs e) { DeleteRequested = MessageBox.Show("Удалить событие?", "Календарь", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes; if (DeleteRequested) DialogResult = true; }
    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private sealed record UserChoice(int Id, string Name) { public override string ToString() => Name; }
}
