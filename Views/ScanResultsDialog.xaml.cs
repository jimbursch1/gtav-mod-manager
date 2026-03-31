using System.Collections.Generic;
using System.Windows;
using GtavModManager.Core;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class ScanResultsDialog : Window
    {
        /// <summary>Populated when the user clicks Import Selected.</summary>
        public List<(string name, ModType type, List<string> files)> ConfirmedImports { get; private set; }

        public ScanResultsDialog(ScanResultsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void ImportSelected_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ScanResultsViewModel;
            if (vm == null) return;

            ConfirmedImports = new List<(string, ModType, List<string>)>();
            foreach (var row in vm.GetSelectedRows())
            {
                // Commit any in-progress cell edit
                row.Result.SuggestedName = row.Name;
                ConfirmedImports.Add((row.Name, row.Type, row.Result.RelativeFiles));
            }

            DialogResult = true;
        }
    }
}
