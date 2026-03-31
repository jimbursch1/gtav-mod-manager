using System.Collections.ObjectModel;
using System.Linq;
using GtavModManager.Core;

namespace GtavModManager.ViewModels
{
    public class ScanResultRowViewModel : ViewModelBase
    {
        private bool _selected;
        private string _name;

        public ScanResult Result { get; }

        public bool Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ModType Type => Result.Type;
        public int FileCount => Result.RelativeFiles?.Count ?? 0;
        public bool AlreadyImported => Result.AlreadyImported;

        public ScanResultRowViewModel(ScanResult result)
        {
            Result = result;
            _name = result.SuggestedName;
            _selected = !result.AlreadyImported;
        }
    }

    public class ScanResultsViewModel : ViewModelBase
    {
        private string _filterText;

        public ObservableCollection<ScanResultRowViewModel> Rows { get; } = new ObservableCollection<ScanResultRowViewModel>();

        public string FilterText
        {
            get => _filterText;
            set
            {
                SetProperty(ref _filterText, value);
                OnPropertyChanged(nameof(FilteredRows));
            }
        }

        public System.Collections.Generic.IEnumerable<ScanResultRowViewModel> FilteredRows =>
            string.IsNullOrWhiteSpace(_filterText)
                ? Rows
                : Rows.Where(r => r.Name.IndexOf(_filterText, System.StringComparison.OrdinalIgnoreCase) >= 0);

        public int SelectedCount => Rows.Count(r => r.Selected);
        public int AlreadyImportedCount => Rows.Count(r => r.AlreadyImported);

        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }

        public ScanResultsViewModel(System.Collections.Generic.List<ScanResult> results)
        {
            foreach (var r in results)
            {
                var row = new ScanResultRowViewModel(r);
                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ScanResultRowViewModel.Selected))
                        OnPropertyChanged(nameof(SelectedCount));
                };
                Rows.Add(row);
            }

            SelectAllCommand = new RelayCommand(() =>
            {
                foreach (var r in Rows.Where(r => !r.AlreadyImported)) r.Selected = true;
            });
            SelectNoneCommand = new RelayCommand(() =>
            {
                foreach (var r in Rows) r.Selected = false;
            });
        }

        /// <summary>Returns the rows the user confirmed for import.</summary>
        public System.Collections.Generic.List<ScanResultRowViewModel> GetSelectedRows() =>
            Rows.Where(r => r.Selected && !r.AlreadyImported).ToList();
    }
}
