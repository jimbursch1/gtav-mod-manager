using System.Windows.Controls;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class GameVersionSnapshotView : UserControl
    {
        public GameVersionSnapshotView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is GameVersionSnapshotViewModel vm)
                vm.CreateSnapshotRequested += ShowCreateSnapshotDialog;
        }

        private void ShowCreateSnapshotDialog()
        {
            var vm = DataContext as GameVersionSnapshotViewModel;
            if (vm == null) return;

            string label = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a label for this snapshot (e.g. \"v1.68 pre-update\"):",
                "Save Game Version Snapshot", "");

            if (label != null)
                vm.ConfirmCreateSnapshot(label);
        }
    }
}
