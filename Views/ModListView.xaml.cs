using System.Windows;
using System.Windows.Controls;
using GtavModManager.Core;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class ModListView : UserControl
    {
        public ModListView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ModListViewModel oldVm)
            {
                oldVm.AddModRequested -= ShowAddModDialog;
                oldVm.ScanRequested -= ShowScanDialog;
            }
            if (e.NewValue is ModListViewModel vm)
            {
                vm.AddModRequested += ShowAddModDialog;
                vm.ScanRequested += ShowScanDialog;
            }
        }

        private void ShowAddModDialog()
        {
            var vm = DataContext as ModListViewModel;
            if (vm == null) return;

            var dialog = new AddModDialog { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
                vm.AddNewMod(dialog.ModName, dialog.ModType, dialog.RelativeFilePaths, vm.GtavRoot ?? "");
        }

        private void ShowScanDialog()
        {
            var vm = DataContext as ModListViewModel;
            if (vm == null) return;

            if (string.IsNullOrEmpty(vm.GtavRoot))
            {
                MessageBox.Show(
                    "GTA V root directory is not configured.\nGo to Settings and set it first.",
                    "Scan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var results = vm.RunScan();
            if (results == null || results.Mods.Count == 0)
            {
                MessageBox.Show("No mods found in the GTA V directory.", "Scan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var scanVm = new ScanResultsViewModel(results);
            var dialog = new ScanResultsDialog(scanVm) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && dialog.ConfirmedImports?.Count > 0)
                vm.BulkImport(dialog.ConfirmedImports);
        }
    }
}
