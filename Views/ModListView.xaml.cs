using System.Collections.Generic;
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
            if (e.NewValue is ModListViewModel vm)
                vm.AddModRequested += ShowAddModDialog;
        }

        private void ShowAddModDialog()
        {
            var vm = DataContext as ModListViewModel;
            if (vm == null) return;

            var dialog = new AddModDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                vm.AddNewMod(
                    dialog.ModName,
                    dialog.ModType,
                    dialog.RelativeFilePaths,
                    (Application.Current.MainWindow?.DataContext as MainViewModel)?.Settings?.GtavRootPath ?? "");
            }
        }
    }
}
