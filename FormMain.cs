using PluginManagerObs.Classes;
using PluginManagerObs.Models;
using System.Diagnostics;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace PluginManagerObs
{
    public partial class FormMain : Form
    {
        ControllerPlugins controllerPlugins = new();
        public FormMain()
        {
            InitializeComponent();
        }

        private void buttonObsPath_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                string selectedPath = folderBrowserDialog.SelectedPath + '\\';
                if (ControllerPlugins.ValidateObsPath(selectedPath))
                {
                    Debug.WriteLine($"OBS found");
                    controllerPlugins.SetObsPath(selectedPath);
                }
                else
                {
                    Debug.WriteLine("Not OBS");
                    DialogResult result2 = MessageBox.Show("Want to use it anyways?", "Not OBS directory", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result2 == DialogResult.Yes)
                    {
                        controllerPlugins.SetObsPath(selectedPath);
                    }
                }
                if (controllerPlugins.PluginsPath != string.Empty)
                {
                    buttonReload.PerformClick();
                }
                labelObsPath.Text = controllerPlugins.GetObsPath();
                controllerPlugins.SavePaths();
            }
        }

        private void buttonPluginsPath_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                controllerPlugins.PluginsPath = folderBrowserDialog.SelectedPath + '\\';
                labelPluginsPath.Text = controllerPlugins.PluginsPath;
                buttonReload.PerformClick();
                controllerPlugins.SavePaths();
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            if (controllerPlugins.LoadPaths())
            {
                labelObsPath.Text = controllerPlugins.GetObsPath();
                labelPluginsPath.Text = controllerPlugins.PluginsPath;
                controllerPlugins.PopulatePlugins();
                PopulateListViewPlugins();
            }
        }

        private void buttonReload_Click(object sender, EventArgs e)
        {
            textBoxSearch.Text = string.Empty;
            listViewPlugins.Items.Clear();
            bool refreshed = controllerPlugins.PopulatePlugins();
            if (refreshed)
            {
                PopulateListViewPlugins();
            }
            else
            {
                MessageBox.Show("Set/Change the Plugins path", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void buttonSearch_Click(object sender, EventArgs e)
        {
            listViewPlugins.Items.Clear();
            controllerPlugins.FilterPlugins(textBoxSearch.Text);
            PopulateListViewPlugins();
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            // Enable multi-Pick
            if (controllerPlugins.GetObsPath() == string.Empty)
            {
                MessageBox.Show("OBS path not set", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (listViewPlugins.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Pick one plugin", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ListViewItem lvi = listViewPlugins.SelectedItems[0];
                    Debug.WriteLine($"Plugin to add: {lvi.Text}");
                    if (controllerPlugins.AddPlugins(lvi.Text))
                    {
                        listViewPlugins.Items.Clear();
                        PopulateListViewPlugins();
                    }
                    else
                    {
                        MessageBox.Show($"Could not add {lvi.Text}\nOther version might be already installed", "Could not add", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void PopulateListViewPlugins()
        {
            foreach (Plugin p in controllerPlugins.ListPlugins)
            {
                ListViewItem lvi = new ListViewItem(p.Name);
                lvi.UseItemStyleForSubItems = false;

                string status = "Not Installed";
                Color bgColor = Color.FromArgb(unchecked((int)0xFF2b5797));
                if (p.IsInstalled)
                {
                    status = "Installed";
                    bgColor = Color.FromArgb(unchecked((int)0xFF1e7145));
                }

                lvi.SubItems.Add(status);
                lvi.SubItems[1].BackColor = bgColor;
                if (p.InstalledDate > 0)
                {
                    lvi.SubItems.Add(DateTimeOffset.FromUnixTimeMilliseconds(p.InstalledDate).LocalDateTime.ToString());
                }
                listViewPlugins.Items.Add(lvi);
            }
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            // Enable multi-Pick
            if (controllerPlugins.GetObsPath() == string.Empty)
            {
                MessageBox.Show("OBS path not set", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (listViewPlugins.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Pick one plugin", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ListViewItem lvi = listViewPlugins.SelectedItems[0];
                    Debug.WriteLine(lvi.Text);
                    if (controllerPlugins.UninstallPlugin(lvi.Text))
                    {
                        listViewPlugins.Items.Clear();
                        PopulateListViewPlugins();
                    }
                    else
                    {
                        MessageBox.Show($"Could not remove {lvi.Text}\nIs OBS running?", "Could not remove", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }

        private void panelDragnDrop_DragDrop(object sender, DragEventArgs e)
        {
            if (controllerPlugins.PluginsPath == string.Empty)
            {
                MessageBox.Show("Set/Change the Plugins path", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        Debug.WriteLine(file);
                        if (!controllerPlugins.CopyPluginZip(file))
                        {
                            MessageBox.Show($"Could not copy file {file}", "Could not copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                //Refresh
                buttonReload.PerformClick();
            }
            panelDragnDrop.Visible = false;
        }

        private void panelDragnDrop_DragEnter(object sender, DragEventArgs e)
        {
            panelDragnDrop.Visible = true;
            e.Effect = DragDropEffects.Copy;
            labelDrop.Visible = true;
            labelDrop.ForeColor = Color.Black;
            labelDrop.Text = "Drop your files to copy!";
            labelDrop.Location = new Point(60, 200);
        }

        private void panelDragnDrop_DragLeave(object sender, EventArgs e)
        {
            labelDrop.ForeColor = Color.DarkRed;
            labelDrop.Text = "Drop to CANCEL";
            labelDrop.Location = new Point(150, 200);
        }

        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {
            panelDragnDrop.Visible = false;
            labelDrop.Visible = false;
        }

        private void FormMain_DragOver(object sender, DragEventArgs e)
        {
            panelDragnDrop.Visible = true;
            labelDrop.Visible = true;
        }

        private void FormMain_DragLeave(object sender, EventArgs e)
        {
            panelDragnDrop.Visible = false;
            labelDrop.Visible = false;
        }
    }
}