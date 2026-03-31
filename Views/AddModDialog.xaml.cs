using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using GtavModManager.Core;
using GtavModManager.ViewModels;

namespace GtavModManager.Views
{
    public partial class AddModDialog : Window
    {
        public string ModName { get; private set; }
        public ModType ModType { get; private set; }
        public List<string> RelativeFilePaths { get; private set; } = new List<string>();

        private string _gtavRoot;

        public AddModDialog()
        {
            InitializeComponent();

            var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
            _gtavRoot = mainVm?.Settings?.GtavRootPath ?? "";
        }

        private void BrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Mod Files",
                Filter = "Mod Files (*.asi;*.dll;*.xml;*.ini)|*.asi;*.dll;*.xml;*.ini|All Files (*.*)|*.*",
                InitialDirectory = string.IsNullOrEmpty(_gtavRoot) ? null : _gtavRoot
            };

            if (dialog.ShowDialog() != true) return;

            var lines = new List<string>(FilesBox.Text.Split(new[] { '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0));

            foreach (var file in dialog.FileNames)
            {
                string relative = file;
                if (!string.IsNullOrEmpty(_gtavRoot) && file.StartsWith(_gtavRoot, StringComparison.OrdinalIgnoreCase))
                    relative = file.Substring(_gtavRoot.Length).TrimStart('\\', '/');

                if (!lines.Contains(relative, StringComparer.OrdinalIgnoreCase))
                    lines.Add(relative);
            }

            FilesBox.Text = string.Join(Environment.NewLine, lines);
        }

        private void AddMod_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Mod name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ModName = name;
            ModType = ParseModType();
            RelativeFilePaths = FilesBox.Text
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            DialogResult = true;
        }

        private ModType ParseModType()
        {
            var selected = TypeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selected?.Tag is string tag && Enum.TryParse<ModType>(tag, out var type))
                return type;
            return ModType.Other;
        }
    }
}
