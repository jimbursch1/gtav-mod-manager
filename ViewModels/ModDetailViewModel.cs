using System.Collections.ObjectModel;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class ModDetailViewModel : ViewModelBase
    {
        private readonly ModInventoryService _inventory;
        private Mod _selectedMod;
        private string _notes;

        public Mod SelectedMod
        {
            get => _selectedMod;
            set
            {
                SetProperty(ref _selectedMod, value);
                LoadDetails(value);
            }
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ObservableCollection<string> FileList { get; } = new ObservableCollection<string>();
        public ObservableCollection<Keybind> KeybindList { get; } = new ObservableCollection<Keybind>();
        public ObservableCollection<ModDependency> DependencyList { get; } = new ObservableCollection<ModDependency>();

        public RelayCommand SaveNotesCommand { get; }

        public ModDetailViewModel(ModInventoryService inventory)
        {
            _inventory = inventory;
            SaveNotesCommand = new RelayCommand(SaveNotes, () => _selectedMod != null);
        }

        private void LoadDetails(Mod mod)
        {
            FileList.Clear();
            KeybindList.Clear();
            DependencyList.Clear();

            if (mod == null)
            {
                Notes = "";
                return;
            }

            Notes = mod.Notes ?? "";

            if (mod.Files != null)
                foreach (var f in mod.Files) FileList.Add(f);

            if (mod.Keybinds != null)
                foreach (var kb in mod.Keybinds) KeybindList.Add(kb);

            if (mod.Dependencies != null)
                foreach (var dep in mod.Dependencies) DependencyList.Add(dep);
        }

        private void SaveNotes()
        {
            if (_selectedMod == null) return;
            _selectedMod.Notes = _notes;
            _inventory.UpdateMod(_selectedMod);
            _inventory.Save();
        }
    }
}
