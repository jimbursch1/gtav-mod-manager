using System.Collections.ObjectModel;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class ProfileManagerViewModel : ViewModelBase
    {
        private readonly ProfileService _profiles;
        private readonly ModInventoryService _inventory;
        private readonly AppSettings _settings;

        private Profile _selectedProfile;
        private bool _isSwitching;
        private string _operationError;

        public ObservableCollection<Profile> Profiles { get; } = new ObservableCollection<Profile>();

        public Profile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                SetProperty(ref _selectedProfile, value);
                SwitchProfileCommand.RaiseCanExecuteChanged();
                DeleteProfileCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsSwitching
        {
            get => _isSwitching;
            private set
            {
                SetProperty(ref _isSwitching, value);
                SwitchProfileCommand.RaiseCanExecuteChanged();
            }
        }

        public string OperationError
        {
            get => _operationError;
            private set => SetProperty(ref _operationError, value);
        }

        public RelayCommand<Profile> SwitchProfileCommand { get; }
        public RelayCommand CreateProfileCommand { get; }
        public RelayCommand<Profile> DeleteProfileCommand { get; }

        public ProfileManagerViewModel(ProfileService profiles, ModInventoryService inventory, AppSettings settings)
        {
            _profiles = profiles;
            _inventory = inventory;
            _settings = settings;

            SwitchProfileCommand = new RelayCommand<Profile>(SwitchProfile,
                p => p != null && !_isSwitching);
            CreateProfileCommand = new RelayCommand(CreateProfile);
            DeleteProfileCommand = new RelayCommand<Profile>(DeleteProfile,
                p => p != null && !_isSwitching);
        }

        public void Reload()
        {
            Profiles.Clear();
            foreach (var p in _profiles.GetAllProfiles())
                Profiles.Add(p);
        }

        private void SwitchProfile(Profile profile)
        {
            if (profile == null) return;
            OperationError = null;
            IsSwitching = true;
            try
            {
                var result = _profiles.SwitchProfile(profile.Id, _inventory.GetAllMods());
                if (!result.Success)
                    OperationError = result.ErrorMessage;
                else
                {
                    _settings.ActiveProfileId = profile.Id;
                    _inventory.Save();
                    _profiles.Save();
                }
            }
            finally
            {
                IsSwitching = false;
            }
        }

        private void CreateProfile()
        {
            // Name dialog handled in code-behind; this exposes the action
            CreateProfileRequested?.Invoke();
        }

        public System.Action CreateProfileRequested;

        public void ConfirmCreateProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var profile = _profiles.CreateProfile(name, _inventory.GetEnabledMods());
            _profiles.Save();
            Profiles.Add(profile);
        }

        private void DeleteProfile(Profile profile)
        {
            if (profile == null) return;
            _profiles.DeleteProfile(profile.Id);
            _profiles.Save();
            Profiles.Remove(profile);
            if (SelectedProfile == profile) SelectedProfile = null;
        }
    }
}
