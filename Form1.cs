using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace FileHistoryRecovery
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnGo_Click(object sender, EventArgs e)
        {
            try
            {
                if (btnGo.Text == "Stop")
                {
                    bgWorker.CancelAsync();
                    return;
                }
                if (lstInputDirectory.Items.Count == 0)
                {
                    MessageBox.Show(this, "Please select an input folder", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (txtOutputDirectory.Text == "")
                {
                    MessageBox.Show(this, "Please select an output folder", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                foreach (string infld in lstInputDirectory.Items)
                {
                    var fld = new DirectoryInfo(infld);
                    if (!fld.Exists)
                    {
                        MessageBox.Show(this, "Cannot access input folder " + fld.FullName, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                var outFolder = new DirectoryInfo(txtOutputDirectory.Text);
                if (!outFolder.Exists)
                {
                    MessageBox.Show(this, "Cannot access output folder " + outFolder.FullName, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (lstInputDirectory.Items.Count > 1 && MessageBox.Show(this, "This will COMBINE the selected folders; continue?", this.Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.Cancel)
                    return;
                btnGo.Text = "Stop";
                btnAddInput.Enabled = false;
                btnRemoveInput.Enabled = false;
                btnSelectOutput.Enabled = false;
                InputFolders = new List<string>();
                foreach (string inFolder in lstInputDirectory.Items)
                {
                    InputFolders.Add(inFolder);
                }
                OutputFolder = txtOutputDirectory.Text;
                bgWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        List<string> InputFolders;
        string OutputFolder;
        private Dictionary<string, MyFileInfo> FileDictionary;
        private Dictionary<string, bool> FolderDictionary;
        private static BackgroundWorker bgWorkerStatic;

        private void bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool success = false;
            try
            {
                bgWorkerStatic = bgWorker;
                if (OutputFolder.EndsWith("\\")) OutputFolder = OutputFolder.Substring(0, OutputFolder.Length - 1);
                FileDictionary = new Dictionary<string, MyFileInfo>();
                FolderDictionary = new Dictionary<string, bool>();
                foreach (var inFolder in InputFolders)
                {
                    AddTree(inFolder);
                }
                long totalSize = 0;
                foreach (var mfi in FileDictionary.Values)
                {
                    totalSize += Math.Max(mfi.FileSize, 1024 * 256);
                }
                long sizeCopied = 0;
                int percentComplete = 0;
                foreach (var mfi in FileDictionary.Values.OrderBy(x => x.RelativePath))
                {
                    if (bgWorker.CancellationPending) return;
                    if (FolderDictionary[mfi.Folder])
                    {
                        FileInfo fi = null;
                        DirectoryInfo di = null;
                        try
                        {
                            fi = new FileInfo(OutputFolder + mfi.RelativePath);
                            di = fi.Directory;
                            if (!di.Exists) di.Create();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Cannot create folder " + di?.FullName + ": " + ex.Message, "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        FolderDictionary[mfi.Folder] = false;
                    }
                    try
                    {
                        mfi.CopyTo(OutputFolder);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Cannot copy file " + mfi.FileInfo.FullName + " to " + OutputFolder + mfi.RelativePath + ": " + ex.Message, "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    sizeCopied += Math.Max(mfi.FileSize, 1024 * 256);
                    int pc2;
                    pc2 = (int)(sizeCopied * 100 / totalSize);
                    if (percentComplete != pc2)
                        bgWorker.ReportProgress(percentComplete = pc2);
                }
                foreach (var fold in FolderDictionary.Where(x => x.Value).OrderBy(x => x.Key).Select(x => x.Key))
                {
                    if (bgWorkerStatic.CancellationPending) return;
                    try
                    {
                        Directory.CreateDirectory(OutputFolder + fold);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Cannot create folder " + OutputFolder + fold + ": " + ex.Message, "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                success = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                FileDictionary = null;
                FolderDictionary = null;
                InputFolders = null;
                OutputFolder = null;
                e.Cancel = !success;
            }
        }

        private void AddTree(string rootFolder)
        {
            if (rootFolder.EndsWith("\\")) rootFolder = rootFolder.Substring(0, rootFolder.Length - 1);
            ProcessFolder(rootFolder, "");
        }
        private void ProcessFolder(string rootFolder, string folder)
        {
            if (bgWorkerStatic.CancellationPending) return;
            FolderDictionary[folder] = true;
            try
            {
                string curFolder = rootFolder + "\\" + folder;
                foreach (string subFolder in Directory.EnumerateDirectories(curFolder))
                {
                    var di = new DirectoryInfo(subFolder);
                    ProcessFolder(rootFolder, folder + '\\' + di.Name);
                }
                foreach (string file in Directory.EnumerateFiles(curFolder))
                {
                    var fi = new FileInfo(file);
                    ProcessFile(rootFolder, folder + "\\" + fi.Name);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot enumerate files/folders in " + rootFolder + "\\" + folder + ": " + ex.Message, "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ProcessFile(string rootFolder, string file)
        {
            try
            {
                MyFileInfo fi = MyFileInfo.Create(rootFolder, file);
                if (fi == null) return;
                MyFileInfo fi2 = null;
                if (FileDictionary.TryGetValue(fi.RelativePath, out fi2))
                {
                    if (fi.BackupDate > fi2.BackupDate)
                    {
                        FileDictionary[fi.RelativePath] = fi2;
                    }
                }
                else
                {
                    FileDictionary.Add(fi.RelativePath, fi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error processing file " + rootFolder + file + ": " + ex.Message, "File History Recovery", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void btnAddInput_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowNewFolderButton = true;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                lstInputDirectory.Items.Add(folderBrowserDialog.SelectedPath);
            }
        }

        private void btnSelectOutput_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.ShowNewFolderButton = true;
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtOutputDirectory.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void btnRemoveInput_Click(object sender, EventArgs e)
        {
            if (lstInputDirectory.SelectedIndex >= 0)
            {
                lstInputDirectory.Items.RemoveAt(lstInputDirectory.SelectedIndex);
            }
        }

        private void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnGo.Text = "Go";
            btnAddInput.Enabled = true;
            btnRemoveInput.Enabled = true;
            btnSelectOutput.Enabled = true;
            MessageBox.Show(this, e.Cancelled ? "Operation cancelled" : "Operation completed", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            progressBar.Value = 0;
        }

        private void bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar.Value = e.ProgressPercentage;
        }
    }
}
