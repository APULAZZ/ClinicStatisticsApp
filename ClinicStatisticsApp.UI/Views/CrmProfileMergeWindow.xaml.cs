using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.ComponentModel;
using System.Windows;

namespace ClinicStatisticsApp.UI.Views;

public partial class CrmProfileMergeWindow : Window
{
    private readonly CurrentUserInfo _user;
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly PatientDirectoryService _service;
    private readonly int _anchorPersonId;
    private List<SelectableCrmProfile> _profiles = [];

    public CrmProfileMergeWindow(int anchorPersonId, CurrentUserInfo user)
    {
        InitializeComponent();
        _anchorPersonId = anchorPersonId;
        _user = user;
        _service = new PatientDirectoryService(_db);
        Loaded += async (_, _) => await LoadAsync();
        Closed += (_, _) => _db.Dispose();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var busy = App.Busy.Begin("Загружаем CRM-карточки для объединения…");
            _profiles = (await _service.GetCrmProfilesForMergeAsync(_anchorPersonId))
                .Select(profile => new SelectableCrmProfile(profile, profile.PersonId == _anchorPersonId)).ToList();
            ProfilesGrid.ItemsSource = _profiles;
            TargetComboBox.ItemsSource = _profiles.Where(x => x.Profile.IsCrmProfile).Select(x => x.Profile).ToList();
            TargetComboBox.SelectedValue = _anchorPersonId;
            if (_profiles.Count < 2)
                MessageBox.Show("Для этого пациента других CRM-карточек или не привязанных карт с теми же фамилией и именем не найдено.", "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    private async void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetComboBox.SelectedValue is not int targetPersonId)
        {
            MessageBox.Show("Выберите основную CRM-карточку по номеру карты.", "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var sourceIds = _profiles.Where(x => x.IsSelected && x.Profile.PersonId is int id && id != targetPersonId).Select(x => x.Profile.PersonId!.Value).ToArray();
        var sourceCardIds = _profiles.Where(x => x.IsSelected && x.Profile.ExternalCardId is not null).Select(x => x.Profile.ExternalCardId!.Value).ToArray();
        if (sourceIds.Length == 0 && sourceCardIds.Length == 0)
        {
            MessageBox.Show("Отметьте хотя бы одну CRM-карточку, которую нужно перенести в основную.", "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show("Карты филиалов, задачи и ссылки на звонки будут перенесены в выбранную основную CRM-карточку. Продолжить?", "Подтвердить объединение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            using var busy = App.Busy.Begin("Объединяем CRM-карточки…");
            await _service.MergeCrmProfilesAsync(targetPersonId, sourceIds, sourceCardIds, _user.UserId);
            MessageBox.Show("CRM-карточки объединены. Источники Firebird не изменялись.", "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Объединение CRM-карточек", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private sealed class SelectableCrmProfile(CrmProfileMergeRow profile, bool isSelected) : INotifyPropertyChanged
    {
        private bool _isSelected = isSelected;
        public CrmProfileMergeRow Profile { get; } = profile;
        public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
