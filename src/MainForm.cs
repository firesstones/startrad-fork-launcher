using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StarTradForLauncher
{
    internal sealed class MainForm : Form
    {
        private readonly ListView installList = new ListView();
        private readonly ListView libraryList = new ListView();
        private readonly Button refreshButton = new Button();
        private readonly Button installButton = new Button();
        private readonly Button uninstallButton = new Button();
        private readonly Button openButton = new Button();
        private readonly Button addLibraryButton = new Button();
        private readonly Button removeLibraryButton = new Button();
        private readonly Label statusLabel = new Label();
        private DetectionResult lastDetection;

        public MainForm()
        {
            Text = "StarTrad";
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 22, 18);
            ForeColor = Color.FromArgb(242, 232, 204);

            BuildLayout();
            Load += async delegate { await RefreshDetectionAsync(); };
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.Padding = new Padding(16);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(root);

            Label title = new Label();
            title.Text = "StarTrad";
            title.Dock = DockStyle.Fill;
            title.Font = new Font(FontFamily.GenericSansSerif, 20, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(226, 190, 104);
            title.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(title, 0, 0);

            ConfigureInstallList();
            root.Controls.Add(installList, 0, 1);

            FlowLayoutPanel actionBar = new FlowLayoutPanel();
            actionBar.Dock = DockStyle.Fill;
            actionBar.FlowDirection = FlowDirection.LeftToRight;
            actionBar.WrapContents = false;
            ConfigureButton(refreshButton, "Rafraichir");
            ConfigureButton(installButton, "Installer / mettre a jour");
            ConfigureButton(uninstallButton, "Desinstaller");
            ConfigureButton(openButton, "Ouvrir dossier");
            refreshButton.Click += async delegate { await RefreshDetectionAsync(); };
            installButton.Click += async delegate { await InstallSelectedAsync(); };
            uninstallButton.Click += async delegate { await UninstallSelectedAsync(); };
            openButton.Click += delegate { OpenSelected(); };
            actionBar.Controls.Add(refreshButton);
            actionBar.Controls.Add(installButton);
            actionBar.Controls.Add(uninstallButton);
            actionBar.Controls.Add(openButton);
            root.Controls.Add(actionBar, 0, 2);

            ConfigureLibraryList();
            root.Controls.Add(libraryList, 0, 3);

            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.ColumnCount = 3;
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ConfigureButton(addLibraryButton, "Ajouter bibliotheque");
            ConfigureButton(removeLibraryButton, "Retirer bibliotheque");
            addLibraryButton.Click += async delegate { await AddLibraryAsync(); };
            removeLibraryButton.Click += async delegate { await RemoveLibraryAsync(); };
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.ForeColor = Color.FromArgb(198, 188, 160);
            footer.Controls.Add(addLibraryButton, 0, 0);
            footer.Controls.Add(removeLibraryButton, 1, 0);
            footer.Controls.Add(statusLabel, 2, 0);
            root.Controls.Add(footer, 0, 4);
        }

        private void ConfigureInstallList()
        {
            installList.Dock = DockStyle.Fill;
            installList.View = View.Details;
            installList.FullRowSelect = true;
            installList.MultiSelect = false;
            installList.BackColor = Color.FromArgb(18, 16, 13);
            installList.ForeColor = Color.FromArgb(242, 232, 204);
            installList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            installList.Columns.Add("Canal", 90);
            installList.Columns.Add("Jeu", 170);
            installList.Columns.Add("Traduction", 170);
            installList.Columns.Add("Derniere FR", 130);
            installList.Columns.Add("Source", 160);
            installList.Columns.Add("Dossier", 520);
        }

        private void ConfigureLibraryList()
        {
            libraryList.Dock = DockStyle.Fill;
            libraryList.View = View.Details;
            libraryList.FullRowSelect = true;
            libraryList.MultiSelect = false;
            libraryList.BackColor = Color.FromArgb(18, 16, 13);
            libraryList.ForeColor = Color.FromArgb(242, 232, 204);
            libraryList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            libraryList.Columns.Add("Bibliotheque detectee", 520);
            libraryList.Columns.Add("Source", 220);
            libraryList.Columns.Add("Etat", 90);
        }

        private void ConfigureButton(Button button, string text)
        {
            button.Text = text;
            button.Height = 30;
            button.AutoSize = true;
            button.Margin = new Padding(0, 6, 8, 6);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Color.FromArgb(58, 45, 30);
            button.ForeColor = Color.FromArgb(255, 241, 201);
            button.FlatAppearance.BorderColor = Color.FromArgb(177, 135, 55);
        }

        private async Task RefreshDetectionAsync()
        {
            SetBusy(true, "Detection en cours...");
            try
            {
                lastDetection = await Task.Run(delegate { return StarTradCore.Detect(); });
                RenderDetection();
                statusLabel.Text = lastDetection.Installs.Count + " version(s) detectee(s)";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erreur : " + ex.Message;
                MessageBox.Show(this, ex.Message, "Detection impossible", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void RenderDetection()
        {
            installList.Items.Clear();
            libraryList.Items.Clear();

            if (lastDetection == null)
            {
                return;
            }

            foreach (StarCitizenInstall install in lastDetection.Installs)
            {
                string game = install.GameVersion ?? "inconnue";
                string trad = install.TranslationInstalled
                    ? "installee " + (install.TranslationVersion ?? "?")
                    : "non installee";
                string latest = install.LatestTranslationVersion ?? "?";

                ListViewItem item = new ListViewItem(install.Channel);
                item.SubItems.Add(game);
                item.SubItems.Add(trad);
                item.SubItems.Add(latest);
                item.SubItems.Add(install.Source ?? "");
                item.SubItems.Add(install.ChannelPath);
                item.Tag = install;
                if (install.TranslationInstalled)
                {
                    item.ForeColor = Color.FromArgb(147, 230, 170);
                }
                installList.Items.Add(item);
            }

            foreach (LibraryCandidate library in lastDetection.Libraries)
            {
                ListViewItem item = new ListViewItem(library.PathValue);
                item.SubItems.Add(library.Source ?? "");
                item.SubItems.Add(library.Valid ? "OK" : "Ignoree");
                item.Tag = library;
                item.ForeColor = library.Valid
                    ? Color.FromArgb(147, 230, 170)
                    : Color.FromArgb(165, 155, 130);
                libraryList.Items.Add(item);
            }
        }

        private StarCitizenInstall SelectedInstall()
        {
            if (installList.SelectedItems.Count == 0)
            {
                return null;
            }
            return installList.SelectedItems[0].Tag as StarCitizenInstall;
        }

        private LibraryCandidate SelectedLibrary()
        {
            if (libraryList.SelectedItems.Count == 0)
            {
                return null;
            }
            return libraryList.SelectedItems[0].Tag as LibraryCandidate;
        }

        private async Task InstallSelectedAsync()
        {
            StarCitizenInstall install = SelectedInstall();
            if (install == null)
            {
                MessageBox.Show(this, "Selectionne une version de Star Citizen.", "StarTrad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Installation de la traduction...");
            try
            {
                await Task.Run(delegate { StarTradCore.InstallTranslation(install); });
                await RefreshDetectionAsync();
                statusLabel.Text = "Traduction installee pour " + install.Channel;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erreur installation";
                MessageBox.Show(this, ex.Message, "Installation impossible", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private async Task UninstallSelectedAsync()
        {
            StarCitizenInstall install = SelectedInstall();
            if (install == null)
            {
                MessageBox.Show(this, "Selectionne une version de Star Citizen.", "StarTrad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "Desinstaller la traduction de " + install.Channel + " ?",
                "StarTrad",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true, "Desinstallation de la traduction...");
            try
            {
                await Task.Run(delegate { StarTradCore.UninstallTranslation(install); });
                await RefreshDetectionAsync();
                statusLabel.Text = "Traduction desinstallee pour " + install.Channel;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erreur desinstallation";
                MessageBox.Show(this, ex.Message, "Desinstallation impossible", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void OpenSelected()
        {
            StarCitizenInstall install = SelectedInstall();
            if (install != null)
            {
                StarTradCore.OpenPath(install.ChannelPath);
                return;
            }

            LibraryCandidate library = SelectedLibrary();
            if (library != null)
            {
                StarTradCore.OpenPath(library.PathValue);
            }
        }

        private async Task AddLibraryAsync()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selectionne le dossier contenant StarCitizen, ou le dossier StarCitizen lui-meme.";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    StarTradCore.AddManualLibrary(dialog.SelectedPath);
                    await RefreshDetectionAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Ajout impossible", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async Task RemoveLibraryAsync()
        {
            LibraryCandidate library = SelectedLibrary();
            if (library == null)
            {
                return;
            }

            StarTradCore.RemoveManualLibrary(library.PathValue);
            await RefreshDetectionAsync();
        }

        private void SetBusy(bool busy, string status)
        {
            refreshButton.Enabled = !busy;
            installButton.Enabled = !busy;
            uninstallButton.Enabled = !busy;
            openButton.Enabled = !busy;
            addLibraryButton.Enabled = !busy;
            removeLibraryButton.Enabled = !busy;
            UseWaitCursor = busy;
            if (status != null)
            {
                statusLabel.Text = status;
            }
        }
    }
}
