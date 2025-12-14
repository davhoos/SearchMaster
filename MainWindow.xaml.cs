using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Threading; // attempting: DispatcherTimer in WPF!

namespace PngFinder
{
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly DispatcherTimer timer = new DispatcherTimer();

        private string _selectedType, _directory, _nameFilter;

        public MainWindow()
        {
            InitializeComponent();
            UpdateDefaultFolder();
            UseWorkFolderCheckBox.IsChecked = false;

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += timer_Tick;
        }

        private void UpdateDefaultFolder()
        {
            string defaultPath;
            if (UseWorkFolderCheckBox.IsChecked == true)
                defaultPath = @"C:\WORK\";
            else
                defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            DirectoryBox.Text = defaultPath;
        }

        private void UseWorkFolderCheckBox_Checked(object sender, RoutedEventArgs e) => UpdateDefaultFolder();
        private void UseWorkFolderCheckBox_Unchecked(object sender, RoutedEventArgs e) => UpdateDefaultFolder();

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
#if NET8_0_OR_GREATER
            var folderDialog = new OpenFolderDialog
            {
                Title = "Vyberte složku",
                InitialDirectory = DirectoryBox.Text
            };
            if (folderDialog.ShowDialog() == true)
                DirectoryBox.Text = folderDialog.FolderName;
#else
            MessageBox.Show("Výběr funguje pouze .NET 8 a novějších.", "Unssuported", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ResultsList.Items.Clear();

            if (FileTypeCombo.SelectedItem is not ComboBoxItem selectedItem)
            {
                MessageBox.Show("Vyberte typ souboru...", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _selectedType = selectedItem.Content.ToString();
            _directory = DirectoryBox.Text.Trim();
            _nameFilter = NameContainsBox.Text.Trim();

            if (!Directory.Exists(_directory))
            {
                _directory = @"C:\Work\";
                if (!Directory.Exists(_directory))
                {
                    MessageBox.Show("Zadaná složka ani výchozí složka C:\\WORK\\ neexistuje.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                DirectoryBox.Text = _directory;
            }
            StartBackgroundWork();
        }

        private void StartBackgroundWork()
        {
            ProgressB.IsIndeterminate = true;
            ProgressB.Visibility = Visibility.Visible;
            worker.RunWorkerAsync(new object[] { _selectedType, _directory, _nameFilter });
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (ProgressB.Value < ProgressB.Maximum)
                ProgressB.Value += 5;
            else
                ProgressB.Value = 0;
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = (object[])e.Argument;
            string selectedType = (string)args[0];
            string directory = (string)args[1];
            string nameFilter = (string)args[2];

            IEnumerable<string> files = Enumerable.Empty<string>();
            if (selectedType == "*.png + *.jpg")
                files = SafeEnumerateFiles(directory, "*.png").Concat(SafeEnumerateFiles(directory, "*.jpg"));
            else if (selectedType == "*.docx + *.pdf")
                files = SafeEnumerateFiles(directory, "*.docx").Concat(SafeEnumerateFiles(directory, "*.pdf"));
            else if (selectedType == "*.zip + *.7z")
                files = SafeEnumerateFiles(directory, "*.zip").Concat(SafeEnumerateFiles(directory, "*.7z"));
            else if (selectedType == "*.mp4 + *.mkv")
                files = SafeEnumerateFiles(directory, "*.mp4").Concat(SafeEnumerateFiles(directory, "*.mkv"));
            else
                files = SafeEnumerateFiles(directory, selectedType);

            var filtered = files.Where(f =>
                File.Exists(f) &&
                Path.GetFileName(f).IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            e.Result = filtered?.ToList();
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ProgressB.IsIndeterminate = false;
            ProgressB.Visibility = Visibility.Collapsed;

            if (e.Error != null)
            {
                MessageBox.Show($"Chyba: {e.Error.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var filtered = (IEnumerable<string>)e.Result;
            if (filtered == null || !filtered.Any())
            {
                MessageBox.Show("Žádný soubor nebyl nalezen.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                foreach (var file in filtered)
                    ResultsList.Items.Add(file);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is string filePath && File.Exists(filePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba při otevírání souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsList.SelectedItem is string filePath && File.Exists(filePath))
            {
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp")
                {
                    try
                    {
                        Process.Start("mspaint", $"\"{filePath}\"");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Chyba při spuštění Malování: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Malování only for obrázky.", "Nepodporovaný formát", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern)
        {
            var files = new List<string>();
            try
            {
                files.AddRange(Directory.EnumerateFiles(root, searchPattern));
            }
            catch (UnauthorizedAccessException) { }
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        files.AddRange(SafeEnumerateFiles(dir, searchPattern));
                    }
                    catch (UnauthorizedAccessException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
            return files;
        }

        private void NameContainsBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search_Click(null, null);
                e.Handled = true;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            NameContainsBox.Clear();
            ResultsList.Items.Clear();
        }
    }
}