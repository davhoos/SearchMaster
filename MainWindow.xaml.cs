using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PngFinder
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            UpdateDefaultFolder();
        }

        private void UpdateDefaultFolder()
        {
            string defaultPath;

            if (UseWorkFolderCheckBox.IsChecked == true)
            {
                defaultPath = @"C:\Work\";
            }
            else
            {
                defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            }

            DirectoryBox.Text = defaultPath;

        }

        private void UseWorkFolderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDefaultFolder();
        }

        private void UseWorkFolderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateDefaultFolder();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
#if NET8_0_OR_GREATER
            var folderDialog = new OpenFolderDialog
            {
                Title = "Vyberte složku",
                InitialDirectory = DirectoryBox.Text
            };

            if (folderDialog.ShowDialog() == true)
            {
                DirectoryBox.Text = folderDialog.FolderName;
            }
#else
            MessageBox.Show("Výběr složky funguje jen v .NET 8 a novějších.", "Nepodporováno", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ResultsList.Items.Clear();

            if (FileTypeCombo.SelectedItem is not ComboBoxItem selectedItem)
            {
                MessageBox.Show("Vyberte typ souboru.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selected = selectedItem.Content.ToString();
            string directory = DirectoryBox.Text.Trim();
            string nameFilter = NameContainsBox.Text.Trim();

            // Fallback logic if folder doesn't exist
            if (!Directory.Exists(directory))
            {
                directory = @"C:\Work\";

                if (!Directory.Exists(directory))
                {
                    MessageBox.Show("Zadaná složka ani výchozí složka C:\\Work\\ neexistuje.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DirectoryBox.Text = directory;
            }

            try
            {
                IEnumerable<string> files = Enumerable.Empty<string>();

                if (selected == "*.png + *.jpg")
                {
                    var pngFiles = SafeEnumerateFiles(directory, "*.png");
                    var jpgFiles = SafeEnumerateFiles(directory, "*.jpg");
                    files = pngFiles.Concat(jpgFiles);
                }
                else if (selected == "*.docx + *.pdf")
                {
                    var docxFiles = SafeEnumerateFiles(directory, "*.docx");
                    var pdfFiles = SafeEnumerateFiles(directory, "*.pdf");
                    files = docxFiles.Concat(pdfFiles);
                }
                else if (selected == "*.zip + *.7z")
                {
                    var zipFiles = SafeEnumerateFiles(directory, "*.zip");
                    var sevenZipFiles = SafeEnumerateFiles(directory, "*.7z");
                    files = zipFiles.Concat(sevenZipFiles);
                }
                else if (selected == "*.mp4 + *.mkv")
                {
                    var mp4Files = SafeEnumerateFiles(directory, "*.mp4");
                    var mkvFiles = SafeEnumerateFiles(directory, "*.mkv");
                    files = mp4Files.Concat(mkvFiles);
                }
                else
                {
                    files = SafeEnumerateFiles(directory, selected);
                }

                var filtered = files.Where(f =>
                    File.Exists(f) &&
                    Path.GetFileName(f).IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                foreach (var file in filtered)
                {
                    ResultsList.Items.Add(file);
                }

                if (!filtered.Any())
                {
                    MessageBox.Show("Žádný soubor nebyl nalezen.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
