using System.Collections.ObjectModel;
using GtavModManager.Core;
using GtavModManager.Services;

namespace GtavModManager.ViewModels
{
    public class GameVersionSnapshotViewModel : ViewModelBase
    {
        private readonly GameVersionSnapshotService _service;

        private GameVersionSnapshot _selectedSnapshot;
        private bool _isWorking;
        private string _operationError;

        public ObservableCollection<GameVersionSnapshot> Snapshots { get; } = new ObservableCollection<GameVersionSnapshot>();

        public GameVersionSnapshot SelectedSnapshot
        {
            get => _selectedSnapshot;
            set
            {
                SetProperty(ref _selectedSnapshot, value);
                RestoreSnapshotCommand.RaiseCanExecuteChanged();
                DeleteSnapshotCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsWorking
        {
            get => _isWorking;
            private set
            {
                SetProperty(ref _isWorking, value);
                CreateSnapshotCommand.RaiseCanExecuteChanged();
                RestoreSnapshotCommand.RaiseCanExecuteChanged();
                DeleteSnapshotCommand.RaiseCanExecuteChanged();
            }
        }

        public string OperationError
        {
            get => _operationError;
            private set => SetProperty(ref _operationError, value);
        }

        public RelayCommand CreateSnapshotCommand { get; }
        public RelayCommand<GameVersionSnapshot> RestoreSnapshotCommand { get; }
        public RelayCommand<GameVersionSnapshot> DeleteSnapshotCommand { get; }

        public System.Action CreateSnapshotRequested;

        public GameVersionSnapshotViewModel(GameVersionSnapshotService service)
        {
            _service = service;

            CreateSnapshotCommand = new RelayCommand(RequestCreate, () => !_isWorking);
            RestoreSnapshotCommand = new RelayCommand<GameVersionSnapshot>(RestoreSnapshot,
                s => s != null && !_isWorking);
            DeleteSnapshotCommand = new RelayCommand<GameVersionSnapshot>(DeleteSnapshot,
                s => s != null && !_isWorking);
        }

        public void Reload()
        {
            Snapshots.Clear();
            foreach (var s in _service.GetAllSnapshots())
                Snapshots.Add(s);
        }

        private void RequestCreate()
        {
            CreateSnapshotRequested?.Invoke();
        }

        public void ConfirmCreateSnapshot(string label)
        {
            OperationError = null;
            IsWorking = true;
            try
            {
                var result = _service.CreateSnapshot(label);
                if (!result.Success)
                {
                    OperationError = result.ErrorMessage;
                    return;
                }
                _service.Save();
                Reload();
            }
            finally
            {
                IsWorking = false;
            }
        }

        private void RestoreSnapshot(GameVersionSnapshot snapshot)
        {
            if (snapshot == null) return;
            OperationError = null;
            IsWorking = true;
            try
            {
                var result = _service.RestoreSnapshot(snapshot.Id);
                if (!result.Success)
                    OperationError = result.ErrorMessage;
            }
            finally
            {
                IsWorking = false;
            }
        }

        private void DeleteSnapshot(GameVersionSnapshot snapshot)
        {
            if (snapshot == null) return;
            var result = _service.DeleteSnapshot(snapshot.Id);
            if (!result.Success)
            {
                OperationError = result.ErrorMessage;
                return;
            }
            _service.Save();
            Snapshots.Remove(snapshot);
            if (SelectedSnapshot == snapshot) SelectedSnapshot = null;
        }
    }
}
