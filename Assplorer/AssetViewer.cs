﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuestomAssets;
using QuestomAssets.AssetsChanger;
using QuestomAssets.BeatSaber;
using QuestomAssets.Utils;
using System.IO;
using System.Text.RegularExpressions;
using QuestomAssets.Models;

namespace Assplorer
{
    public partial class AssetViewer : Form
    {
        public AssetViewer()
        {
            InitializeComponent();
            Log.SetLogSink(new TextBoxLogSink(tbLog));
        }

        AssetsManager _manager;
        IFileProvider _fileProvider;


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_fileProvider != null)
                _fileProvider.Dispose();
        }
        private void CloseStuff()
        {
            etMain.DataSource = null;
            etLeft.DataSource = null;
            etRight.DataSource = null;
            cbAssetsFile.Items.Clear();

            if (_fileProvider != null)
            {
                _fileProvider.Dispose();
                _fileProvider = null;
            }
            hexSearch = "";
        }


        private void BtnLoad_Click(object sender, EventArgs e)
        {
            ContextMenu cm = new ContextMenu(new MenuItem[]
            {
                new MenuItem("APK", (s, e2) =>
                {
                    OpenFileDialog ofd = new OpenFileDialog()
                    {
                         CheckFileExists = true,
                         Title = "Open Bundle File",
                         Multiselect = false
                    };
                    if (ofd.ShowDialog() == DialogResult.Cancel)
                        return;
                    CloseStuff();
                    try
                    {
                        _fileProvider = new ZipFileProvider(ofd.FileName, FileCacheMode.Memory,false);
                        _manager = new AssetsManager(_fileProvider, BSConst.KnownFiles.AssetsRootPath, BSConst.GetAssetTypeMap());
                        if (_fileProvider.FindFiles("globalgamemanagers").Count > 0)
                            _manager.GetAssetsFile("globalgamemanagers.assets");
                        if (_fileProvider.FindFiles("globalgamemanagers.assets*").Count > 0)
                            _manager.GetAssetsFile("globalgamemanagers.assets");
                        _manager.FindAndLoadAllAssets();
                        FillAssetsFiles();
                        this.Text = "Assets Explorer - " + Path.GetFileName(ofd.FileName);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr("Couldn't load APK!", ex);
                        MessageBox.Show("Failed to load!");
                        if (_fileProvider != null)
                        {
                            _fileProvider.Dispose();
                            _fileProvider = null;
                        }
                        return;
                    }
                }),
                new MenuItem("Folder", (s, e2) =>
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog()
                    {
                         ShowNewFolderButton = false,
                         Description = "Select Assets Root Folder"
                    };
                    if (fbd.ShowDialog() == DialogResult.Cancel)
                        return;
                    CloseStuff();
                    try
                    {
                        _fileProvider = new FolderFileProvider(fbd.SelectedPath, false);
                        _manager = new AssetsManager(_fileProvider,"", BSConst.GetAssetTypeMap());
                        if (_fileProvider.FindFiles("globalgamemanagers").Count > 0)
                            _manager.GetAssetsFile("globalgamemanagers.assets");
                        if (_fileProvider.FindFiles("globalgamemanagers.assets*").Count > 0)
                            _manager.GetAssetsFile("globalgamemanagers.assets");
                        _manager.FindAndLoadAllAssets();
                        FillAssetsFiles();
                        this.Text = "Assets Explorer - " + Path.GetFileName(fbd.SelectedPath);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr("Couldn't load folder!", ex);
                        MessageBox.Show($"Failed to load!\nException: {ex}");
                        if (_fileProvider != null)
                        {
                            _fileProvider.Dispose();
                            _fileProvider = null;
                        }
                        return;
                    }
                }),
                new MenuItem("Bundle", (s, e2) =>
                {
                    OpenFileDialog ofd = new OpenFileDialog()
                    {
                         CheckFileExists = true,
                         Title = "Open Bundle File",
                         Multiselect = false
                    };
                    if (ofd.ShowDialog() == DialogResult.Cancel)
                        return;
                    CloseStuff();
                    try
                    {
                        _fileProvider = new BundleFileProvider(ofd.FileName,true);
                        _manager = new AssetsManager(_fileProvider, "", BSConst.GetAssetTypeMap());
                        _manager.FindAndLoadAllAssets();
                        FillAssetsFiles();
                        this.Text = "Assets Explorer - " + Path.GetFileName(ofd.FileName);
                    }
                    catch (Exception ex)
                    {
                        Log.LogErr("Couldn't load bundle!", ex);
                        MessageBox.Show("Failed to load!");
                        if (_fileProvider != null)
                        {
                            _fileProvider.Dispose();
                            _fileProvider = null;
                        }
                        return;
                    }
                })
            });
            cm.Show(btnLoad, new Point(0, btnLoad.Height));
            return;

            

        }

        public class TextBoxLogSink : ILog
        {
            private TextBox _textbox;
            public TextBoxLogSink(TextBox textbox)
            {
                _textbox = textbox;
            }
            public void LogErr(string message, Exception ex)
            {
                _textbox.Text = $"ERROR: {message} {ex.Message} {ex.StackTrace}\r\n\r\n" + _textbox.Text;
            }

            public void LogErr(string message, params object[] args)
            {
                _textbox.Text = "ERROR: " + string.Format(message, args) + "\r\n" + _textbox.Text;
            }

            public void LogMsg(string message, params object[] args)
            {
                _textbox.Text = "Msg: " + string.Format(message, args) + "\r\n" + _textbox.Text;
            }
        }

        private void FillAssetsFiles()
        {
            etMain.DataSource = null;
            cbAssetsFile.Items.Clear();
            cbAssetsFile.Items.Add("* All *");
            
            foreach (var openFile in _manager.OpenFiles.OrderBy(x => x.AssetsFilename))
            {
                cbAssetsFile.Items.Add(openFile.AssetsFilename);
            }
            
            cbAssetsFile.SelectedIndex = 0;
        }

        private void CbAssetsFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            etMain.DataSource = null;
            if (cbAssetsFile.SelectedItem == null)
                return;

            if ((string)cbAssetsFile.SelectedItem == "")
                return;
            List<Tuple<string, IEnumerable>> list = new List<Tuple<string, IEnumerable>>();
            Node n = null;
            if ((string)cbAssetsFile.SelectedItem == "* All *")
            {
                n = Node.MakeNode(_manager);
                //foreach (var file in _manager.OpenFiles)
                //{
                //    list.Add(new Tuple<string, IEnumerable>(file.AssetsFileName, file.Metadata.ObjectInfos.Select(x => x.Object)));
                //}
            }
            else
            {
                var assetsFile = _manager.GetAssetsFile(cbAssetsFile.SelectedItem.ToString());
                //list.Add(new Tuple<string, IEnumerable>(assetsFile.AssetsFileName, assetsFile.Metadata.ObjectInfos.Select(x => x.Object)));
                n = Node.MakeNode(assetsFile);
            }
            etMain.DataSource = n;
        }

        private void BtnClearLog_Click(object sender, EventArgs e)
        {
            tbLog.Text = "";
        }

        private void TestCloneObject(AssetsObject ao)
        {
            try
            {
                var cloned = ao.ObjectInfo.DeepClone();
                Node nOrig = Node.MakeNode(ao);
                Node nCloned = Node.MakeNode(cloned);
                etLeft.DataSource = nOrig;
                etRight.DataSource = nCloned;
                tabControl1.SelectedTab = tpCompare;
            }
            catch (Exception ex)
            {
                Log.LogErr("Failed to clone object!", ex);
                MessageBox.Show("Failed to even clone!");
            }
        }

        private void EtMain_NodeRightClicked(object sender, TreeNodeMouseClickEventArgs e)
        {
            var cm = new ContextMenu();
            var n = e.Node.Tag as Node;
            if (n != null)
            {
                var ao = n.Obj as AssetsObject;
                if (ao != null)
                {                    
                    if (n.Obj is AssetsObject)
                    {
                        cm.MenuItems.Add(new MenuItem("Test Clone", (o, ea) => { TestCloneObject(ao); }));
                    }
                    if (n.StubToNode != null)
                    {
                        cm.MenuItems.Add(new MenuItem("Go to Object", (o, ea) =>
                        {
                            var tn = n.StubToNode.ExtRef as TreeNode;
                            if (tn != null)
                            {
                                tn.EnsureVisible();
                                etMain.SelectedNode = tn;
                            }
                        }));
                    }
                }
            }
            
            if (cm.MenuItems.Count > 0)
                cm.Show(sender as Control, etMain.PointToClient(Cursor.Position));
        }

        private void BtnNewWindow_Click(object sender, EventArgs e)
        {
            AssetViewer vwr = new AssetViewer();
            vwr.Show();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            _manager.WriteAllOpenAssets();
            _fileProvider.Save();
            if (_fileProvider is ZipFileProvider)
            {
                ApkSigner s = new ApkSigner(BSConst.DebugCertificatePEM);
                s.Sign(_fileProvider);
            }
 
            
            CloseStuff();
        }
        private Node selectedNode = null;
        private void EtMain_NodeSelected(object sender, Node e)
        {
            if (e.Obj == null)
            {
                selectedNode = null;
                pgAssetProps.SelectedObject = null;
            } else
            {
                selectedNode = e;
                pgAssetProps.SelectedObject = e.Obj;
            }
        }

        private void PgAssetProps_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (selectedNode != null)
            {
                try
                {
                    string nodeName;
                    string typeName;
                    Node.MakeNodeAndTypeName(selectedNode.Obj, out nodeName, out typeName);
                    selectedNode.Text = nodeName;
                    var tn = selectedNode.ExtRef as TreeNode;
                    if (tn != null)
                        tn.Text = selectedNode.Text;
                }
                catch (Exception ex)
                {
                    Log.LogErr($"Exception updating node text", ex);
                }
            }
        }

        private string hexSearch = "";
        private Node originalSource;
        private void Button1_Click(object sender, EventArgs e)
        {
            // Workflow:
            // Ask for binary
            // Search all assets for 4 byte match, save matching assets
            // Search each assetobject ONLY, NOT the entire file
            // Open dialog
            hexSearch = Prompt.ShowDialog("Enter hex string to search for: ", "Binary Search", hexSearch).Replace("-", "").ToUpper();
            if (hexSearch.Length % 2 != 0)
                hexSearch += "0";
            if (originalSource == null)
            {
                // Only set original if we haven't already.
                originalSource = etMain.DataSource.DeepCopy();
            }
            // What index are we? **ALL** or single file?
            // If ALL... We need to check all files. Each node is a file.
            // If not, We check each asset individually.
            if ((string)cbAssetsFile.SelectedItem == "* All *")
            {
                for (int i = 0; i < etMain.DataSource.Nodes.Count; i++)
                {
                    var f = etMain.DataSource.Nodes[i];
                    for (int j = 0; j < f.Nodes.Count; j++)
                    {
                        var n = f.Nodes[j];
                        if (n.Obj is AssetsObject && (n.Obj as AssetsObject).ContainsHex(hexSearch))
                        {
                            f.Nodes.RemoveAt(j);
                            j--;
                            n.Parent.Nodes.Remove(n);
                        }
                    }
                }
            }
            for (int i = 0; i < etMain.DataSource.Nodes.Count; i++)
            {
                var n = etMain.DataSource.Nodes[i];
                if (n.Obj is AssetsObject && (n.Obj as AssetsObject).ContainsHex(hexSearch))
                {
                    etMain.DataSource.Nodes.RemoveAt(i);
                    i--;
                    n.Parent.Nodes.Remove(n);
                }
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            // Reset the binary search
            hexSearch = "";
            etMain.DataSource = originalSource;
            originalSource = null;
        }
    }

    static class Ext
    {
        public static bool ContainsHex(this AssetsObject aob, string hex)
        {
            byte[] buffer;
            using (var s = new BinaryReader(aob.ObjectInfo.ParentFile.FileProvider.GetReadStream(aob.ObjectInfo.ParentFile.AssetsFilename)))
            {
                buffer = s.ReadBytes(aob.GetSize());
            }
            return buffer.ContainsSequence(hex.StringToByteArray());
        }
        public static bool ContainsSequence(this byte[] arr, byte[] seq)
        {
            if (seq.Length > arr.Length)
                return false;
            for (int i = 0; i < arr.Length - seq.Length + 1; i++)
            {
                byte[] temp = new byte[seq.Length];
                Array.Copy(arr, i, temp, 0, seq.Length);
                if (seq.SequenceEqual(temp))
                    return true;
            }
            return false;
        }
        public static byte[] StringToByteArray(this string hex)
        {
            if (hex.Length % 2 != 0)
                return null;
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
