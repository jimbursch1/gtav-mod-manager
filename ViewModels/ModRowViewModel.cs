using GtavModManager.Core;

namespace GtavModManager.ViewModels
{
    /// <summary>
    /// Lightweight DataGrid row wrapper for a Mod. Does not expose the full Mod directly
    /// to keep the data model clean from UI concerns.
    /// </summary>
    public class ModRowViewModel : ViewModelBase
    {
        private bool _hasConflicts;

        public Mod Mod { get; }

        public string Id => Mod.Id;
        public string Name => Mod.Name;
        public string Version => Mod.Version;
        public string Author => Mod.Author;
        public ModType Type => Mod.Type;
        public ModStatus Status => Mod.Status;
        public int LoadOrder => Mod.LoadOrder;

        public bool HasConflicts
        {
            get => _hasConflicts;
            set => SetProperty(ref _hasConflicts, value);
        }

        public ModRowViewModel(Mod mod)
        {
            Mod = mod;
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(LoadOrder));
        }
    }
}
