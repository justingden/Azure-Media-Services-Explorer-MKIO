﻿// ----------------------------------------------------------------------------------------------
//    Copyright 2023 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//---------------------------------------------------------------------------------------------


using AMSExplorer.ManifestGeneration;
using AMSExplorer.Rest;
using Azure;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.DataMovement;
using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AMSExplorer
{
    public partial class AssetInformation : Form
    {
        private const string texttrack = "Text track";
        private const string videotrack = "Video track";
        private const string audiotrack = "Audio track";
        private MediaAssetResource _asset;
        private readonly AMSClientV3 _amsClient;
        private IEnumerable<StreamingEndpointResource> _streamingEndpoints;
        private readonly Mainform myMainForm;
        private bool oktobuildlocator = false;
        private ManifestTimingData myassetmanifesttimingdata = null;
        private CloudBlobContainer container = null;
        private List<IListBlobItem> blobs = null;
        private List<StreamingLocatorContentKey> contentKeysForCurrentLocator;
        private Uri _containerSasUrl = null;
        private string _serverManifestName = null;
        private AmsClientRest _restClient;

        public AssetInformation(Mainform mainform, AMSClientV3 amsClient, MediaAssetResource asset, IEnumerable<StreamingEndpointResource> streamingEndpoints)
        {
            InitializeComponent();
            Icon = Bitmaps.Azure_Explorer_ico;
            myMainForm = mainform;
            _amsClient = amsClient;
            _asset = asset;
            _streamingEndpoints = streamingEndpoints;
            _restClient = new AmsClientRest(_amsClient);
        }

        private void ToolStripMenuItemCopy_Click(object sender, EventArgs e)
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    System.Windows.Forms.Clipboard.SetText(TreeViewLocators.SelectedNode.Text);
                }
            }
        }

        private async void ToolStripMenuItemDASHIF_Click(object sender, EventArgs e)
        {
            await DoDASHIFPlayerAsync();
        }

        private async Task DoDASHIFPlayerAsync()
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    switch (TreeViewLocators.SelectedNode.Parent.Text)
                    {
                        case AssetTools._dash_cmaf:
                        case AssetTools.format_dash_csf:
                            await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: PlayerType.DASHIFRefPlayer, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, mainForm: myMainForm);
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private void ContextMenuStripLocators_Opening(object sender, CancelEventArgs e)
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    toolStripMenuItemAzureMediaPlayer.Enabled = toolStripMenuItemAdvPlayer.Enabled = false;
                    toolStripMenuItemDASHIF.Enabled = false;
                    toolStripMenuItemOpen.Enabled = false;
                    deleteLocatorToolStripMenuItem.Enabled = false;

                    if (TreeViewLocators.SelectedNode.Parent.Text.Equals(AssetTools._smooth) || TreeViewLocators.SelectedNode.Parent.Text.Contains(AssetTools._smooth_legacy))
                    {
                        toolStripMenuItemAzureMediaPlayer.Enabled = toolStripMenuItemAdvPlayer.Enabled = true;
                        toolStripMenuItemDASHIF.Enabled = false;
                        toolStripMenuItemOpen.Enabled = false;
                    }
                    if (TreeViewLocators.SelectedNode.Parent.Text.Equals(AssetTools._dash_csf) || (TreeViewLocators.SelectedNode.Parent.Text.Equals(AssetTools._dash_cmaf)))
                    {
                        toolStripMenuItemAzureMediaPlayer.Enabled = toolStripMenuItemAdvPlayer.Enabled = true;
                        toolStripMenuItemDASHIF.Enabled = true;
                        toolStripMenuItemOpen.Enabled = false;
                    }
                    if (TreeViewLocators.SelectedNode.Parent.Text.Equals(AssetTools._prog_down_https_SAS))
                    {
                        toolStripMenuItemAzureMediaPlayer.Enabled = toolStripMenuItemAdvPlayer.Enabled = (TreeViewLocators.SelectedNode.Text.ToLower().Contains(".mp4"));
                        toolStripMenuItemDASHIF.Enabled = false;
                        toolStripMenuItemOpen.Enabled = true;
                    }
                    if (TreeViewLocators.SelectedNode.Parent.Text.Equals(AssetTools._prog_down_http_streaming))
                    {
                        toolStripMenuItemAzureMediaPlayer.Enabled = toolStripMenuItemAdvPlayer.Enabled = (TreeViewLocators.SelectedNode.Text.ToLower().Contains(".mp4"));
                        toolStripMenuItemDASHIF.Enabled = false;
                        toolStripMenuItemOpen.Enabled = !(TreeViewLocators.SelectedNode.Text.ToLower().Contains(".ism"));
                    }
                }
                else
                {
                    deleteLocatorToolStripMenuItem.Enabled = true; // no parent, so we can propose the deletion
                }
            }
        }

        private async void ToolStripMenuItemPlaybackMP4_Click(object sender, EventArgs e)
        {
            await DoAdvcTestPlayerAsync();
        }

        private async Task DoAdvcTestPlayerAsync()
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: PlayerType.AdvancedTestPlayer, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, mainForm: myMainForm);
                }
            }
        }

        private void ToolStripMenuItemOpen_Click(object sender, EventArgs e)
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = TreeViewLocators.SelectedNode.Text,
                            UseShellExecute = true
                        }
                    };
                    p.Start();
                }
            }
        }

        private async Task ListAssetBlobsAsync()
        {
            bool proposeListBlobsInDir = false;
            if (container == null) //first time
            {
                MediaAssetStorageContainerSasContent content = new()
                {
                    Permissions = MediaAssetContainerPermission.ReadWriteDelete,
                    ExpireOn = DateTime.Now.AddHours(2).ToUniversalTime()
                };

                /*
                ListContainerSasInput input = new()
                {
                    Permissions = AssetContainerPermission.ReadWriteDelete,
                    ExpiryTime = DateTime.Now.AddHours(2).ToUniversalTime()
                };
                */

                //AssetContainerSas response;
                Pageable<Uri> response;
                try
                {
                    response = _asset.GetStorageContainerUris(content);
                    //response = await _amsClient.AMSclient.Assets.ListContainerSasAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, _asset.Name, input.Permissions, input.ExpiryTime);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //string uploadSasUrl = response.AssetContainerSasUrls.First();
                // Uri sasUri = new(uploadSasUrl);

                container = new CloudBlobContainer(response.First());
            }

            /*
            var keys = _amsClient.GetStorageKeys(myAssetV3.StorageAccountName);

            CloudStorageAccount storageAccount;
            storageAccount = new CloudStorageAccount(new StorageCredentials(myAssetV3.StorageAccountName, keys.StorageAccountKeys.Key1), _amsClient.environment.ReturnStorageSuffix(), true);
            var cloudBlobClient = storageAccount.CreateCloudBlobClient();

            var container = cloudBlobClient.GetContainerReference(myAssetV3.Container);
            */

            listViewBlobs.Items.Clear();
            DGFiles.Rows.Clear();
            listViewBlobs.BeginUpdate();

            bool serverManifestPresent = false;
            bool clientManifestPresent = false;
            _serverManifestName = null;

            BlobContinuationToken continuationToken = null;
            blobs = new List<IListBlobItem>();

            do
            {
                BlobResultSegment segment = await container.ListBlobsSegmentedAsync(null, checkBoxListBlobsDirectories.Visible && checkBoxListBlobsDirectories.Checked, BlobListingDetails.Metadata, null, continuationToken, null, null);
                blobs.AddRange(segment.Results);

                foreach (IListBlobItem blob in segment.Results)
                {
                    if (blob is CloudBlockBlob bl)
                    {
                        ListViewItem item = new(bl.Name, 0);
                        if (bl.Name.ToLower().EndsWith(".ism"))
                        {
                            serverManifestPresent = true;
                            _serverManifestName = bl.Name;
                        }
                        else if (bl.Name.ToLower().EndsWith(".ismc"))
                        {
                            clientManifestPresent = true;
                        }

                        if (bl.Properties.Length == 0)
                        {
                            item.ForeColor = Color.Red;
                        }
                        /*
                        if (file.AssetFileOptions == AssetFileOptions.Fragmented)
                        {
                            item.ForeColor = Color.DarkGoldenrod;
                        }
                        */
                        item.SubItems.Add(AssetTools.FormatByteSize(bl.Properties.Length));

                        listViewBlobs.Items.Add(item);
                        //size += file.ContentFileSize;
                    }
                    else if (blob is CloudBlobDirectory blobd)
                    {
                        proposeListBlobsInDir = true;
                        ListViewItem item = new(blobd.Prefix, 0)
                        {
                            ForeColor = Color.DarkGoldenrod
                        };
                        // let comment as it can be time expensive to the math
                        //item.SubItems.Add(AssetInfo.FormatByteSize(AssetInfo.GetSizeBlobDirectory(bl)));
                        listViewBlobs.Items.Add(item);
                    }

                }

                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);

            listViewBlobs.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            if (listViewBlobs.Items.Count > 0) listViewBlobs.Items[0].Selected = true;
            listViewBlobs.EndUpdate();

            checkBoxListBlobsDirectories.Visible = proposeListBlobsInDir || checkBoxListBlobsDirectories.Visible;

            buttonGenerateServerManifest.Enabled = !serverManifestPresent;
            buttonGenerateClientManifest.Enabled = serverManifestPresent && !clientManifestPresent;
        }


        private async Task ListAssetTracksAsync()
        {

            // IEnumerable<AssetTrack> response;

            try
            {
                var response = _asset.GetMediaAssetTracks().GetAllAsync();

                listViewTracks.Items.Clear();
                dGTracks.Rows.Clear();
                listViewTracks.BeginUpdate();

                await foreach (var track in response)
                {
                    ListViewItem item = new(track.Data.Name, 0);
                    var tbase = track.Data.Track;
                    if (tbase is AudioTrack at)
                    {
                        item.SubItems.Add(audiotrack);
                    }
                    else if (tbase is VideoTrack vt)
                    {
                        item.SubItems.Add(videotrack);
                    }
                    else if (tbase is TextTrack tt)
                    {
                        item.SubItems.Add(texttrack);
                    }
                    listViewTracks.Items.Add(item);
                }
                listViewTracks.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
            }
            catch (RequestFailedException ex) when (ex.Status == ((int)System.Net.HttpStatusCode.Conflict))
            {
                // No tracks
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            finally
            {
                listViewTracks.EndUpdate();
            }
        }

        /*
        private async Task<List<string>> ReturnTexttracksNamesAsync()
        {
            List<string> listTracks = new();

            try
            {
                var response = _asset.GetMediaAssetTracks().GetAllAsync();

                await foreach (var track in response)
                {
                    var tbase = track.Data.Track;
                    if (tbase is TextTrack tt)
                    {
                        listTracks.Add(track.Data.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return listTracks;
        }
        */

        private async Task<List<string>> ReturnTracksNamesAsync(Type typeTrack) // = typeof( TextTrack)
        {
            List<string> listTracks = new();

            try
            {
                var response = _asset.GetMediaAssetTracks().GetAllAsync();

                await foreach (var track in response)
                {
                    var tbase = track.Data.Track;
                    if (tbase.GetType() == typeTrack)
                    {
                        listTracks.Add(track.Data.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return listTracks;
        }


        private async void AssetInformation_Load(object sender, EventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            // DpiUtils.InitPerMonitorDpi(this);

            labelAssetNameTitle.Text += _asset.Data.Name;

            DGAsset.ColumnCount = 2;
            DGFiles.ColumnCount = 2;
            DGFiles.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;
            dGTracks.ColumnCount = 2;
            dGTracks.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;
            dataGridViewKeys.ColumnCount = 2;
            dataGridViewKeys.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;

            // asset info
            DGAsset.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;
            DGAsset.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name, _asset.Data.Name);
            DGAsset.Rows.Add("Description", _asset.Data.Description);
            DGAsset.Rows.Add("Id", _asset.Data.Id);
            DGAsset.Rows.Add("AlternateId", _asset.Data.AlternateId);
            DGAsset.Rows.Add("AssetId", _asset.Data.AssetId);
            DGAsset.Rows.Add("Container", _asset.Data.Container);
            DGAsset.Rows.Add("StorageAccountName", _asset.Data.StorageAccountName);
            DGAsset.Rows.Add("StorageEncryptionFormat", _asset.Data.StorageEncryptionFormat);

            AssetInfoData MyAssetTypeInfo = await AssetTools.GetAssetTypeAsync(_asset, _amsClient);
            if (MyAssetTypeInfo != null)
            {
                DGAsset.Rows.Add("Type", MyAssetTypeInfo.Type);
                DGAsset.Rows.Add("Size", AssetTools.FormatByteSize(MyAssetTypeInfo.Size));
            }

            DGAsset.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Created, _asset.Data.CreatedOn?.DateTime.ToLocalTime().ToString("G"));
            DGAsset.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_LastModified, _asset.Data.LastModifiedOn?.DateTime.ToLocalTime().ToString("G"));

            if (_streamingEndpoints == null)
            {
                _streamingEndpoints = await _amsClient.AMSclient.GetStreamingEndpoints().GetAllAsync().ToListAsync();
            }

            foreach (var se in _streamingEndpoints)
            {
                comboBoxStreamingEndpoint.Items.Add(new Item(string.Format(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_012ScaleUnit, se.Data.Name, se.Data.ResourceState, StreamingEndpointInformation.ReturnTypeSE(se)), se.Data.HostName));
                if (se.Data.Name == "default")
                {
                    comboBoxStreamingEndpoint.SelectedIndex = comboBoxStreamingEndpoint.Items.Count - 1;
                }

                foreach (string custom in se.Data.CustomHostNames)
                {
                    comboBoxStreamingEndpoint.Items.Add(new Item(string.Format(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_012ScaleUnitCustomHostname3, se.Data.Name, se.Data.ResourceState, StreamingEndpointInformation.ReturnTypeSE(se), custom), custom));
                }
            }
            // if no SE has been selected (there is no SE named "default") then let's select the fist in the list
            if (_streamingEndpoints.Any() && comboBoxStreamingEndpoint.SelectedIndex == -1)
            {
                comboBoxStreamingEndpoint.SelectedIndex = 0;
            }
            oktobuildlocator = true;

            return;
        }

        private async Task DisplayAssetFiltersAsync()
        {
            //            List<AssetFilter> assetFilters = new();
            var assetFilters = _asset.GetMediaAssetFilters().GetAllAsync();


            dataGridViewFilters.ColumnCount = 6;
            dataGridViewFilters.Columns[0].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name;
            dataGridViewFilters.Columns[0].Name = AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name;
            dataGridViewFilters.Columns[1].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_TrackRules;
            dataGridViewFilters.Columns[1].Name = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_Rules;
            dataGridViewFilters.Columns[2].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_StartDHMS;
            dataGridViewFilters.Columns[2].Name = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_Start;
            dataGridViewFilters.Columns[3].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_EndDHMS;
            dataGridViewFilters.Columns[3].Name = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_End;
            dataGridViewFilters.Columns[4].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_DVRDHMS;
            dataGridViewFilters.Columns[4].Name = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_DVR;
            dataGridViewFilters.Columns[5].HeaderText = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_LiveBackoffDHMS;
            dataGridViewFilters.Columns[5].Name = AMSExplorer.Properties.Resources.AssetInformation_DisplayAssetFilters_LiveBackoff;

            dataGridViewFilters.Rows.Clear();

            if (/*assetFilters.Count > 0 &&*/ myassetmanifesttimingdata == null)
            {

                // let's read manifest data
                XDocument manifest = null;
                try
                {
                    manifest = await AssetTools.TryToGetClientManifestContentAsABlobAsync(_asset, _amsClient);
                }
                catch
                {
                }

                if (manifest == null)
                {
                    try
                    {
                        manifest = await AssetTools.TryToGetClientManifestContentUsingStreamingLocatorAsync(_asset, _amsClient);
                    }
                    catch
                    {

                    }
                }

                if (manifest != null)
                {
                    myassetmanifesttimingdata = AssetTools.GetManifestTimingData(manifest);
                }
            }

            await foreach (var filter in assetFilters)
            {
                string s = null;
                string e = null;
                string d = null;
                string l = null;

                if (filter.Data.PresentationTimeRange != null)
                {
                    long? start = filter.Data.PresentationTimeRange.StartTimestamp;
                    long? end = filter.Data.PresentationTimeRange.EndTimestamp;
                    long? dvr = filter.Data.PresentationTimeRange.PresentationWindowDuration;
                    long? backoff = filter.Data.PresentationTimeRange.LiveBackoffDuration;

                    double dscale = (filter.Data.PresentationTimeRange.Timescale != null) ?
                        (double)filter.Data.PresentationTimeRange.Timescale
                        : TimeSpan.TicksPerSecond;

                    double dscaleoffset = (!myassetmanifesttimingdata.Error && myassetmanifesttimingdata.TimeScale != null) ?
                        (double)myassetmanifesttimingdata.TimeScale
                        : TimeSpan.TicksPerSecond;

                    s = ReturnFilterTextWithOffSet(start, dscale, myassetmanifesttimingdata.TimestampOffset, dscaleoffset, "min");
                    e = ReturnFilterTextWithOffSet(end, dscale, myassetmanifesttimingdata.TimestampOffset, dscaleoffset, "max");
                    d = ReturnFilterTextWithOffSet(dvr, dscale, 0, dscaleoffset, "max");
                    l = ReturnFilterTextWithOffSet(backoff, dscale, 0, dscaleoffset, "min");
                }
                try
                {
                    int rowi = dataGridViewFilters.Rows.Add(filter.Data.Name, filter.Data.Tracks.Count, s, e, d, l);
                }
                catch
                {
                    int rowi = dataGridViewFilters.Rows.Add(filter.Data.Name, "Error", s, e, d, l);
                }
            }
        }

        private static string ReturnFilterTextWithOffSet(string value, ulong offset, double scale)
        {
            ulong valueint = ulong.Parse(value);
            if (valueint == ulong.MaxValue)
            {
                return "max";
            }
            else
            {
                valueint -= offset;
                return TimeSpan.FromTicks((long)(valueint / scale)).ToString(@"d\.hh\:mm\:ss");
            }
        }

        private static string ReturnFilterTextWithOffSet(long? value, double scalevalue, ulong offset, double scaleoffset, string defaultwhennull)
        {

            if (value == null)
            {
                return defaultwhennull;
            }
            else
            {
                double value2 = (double)value / scalevalue;
                double offset2 = offset / scaleoffset;
                value2 -= offset2;
                return TimeSpan.FromSeconds(value2).ToString(@"d\.hh\:mm\:ss");
            }
        }

        private static string ReturnFilterText(TimeSpan? value, string defaultwhennull)
        {

            if (value == null)
            {
                return defaultwhennull;
            }
            else
            {
                return ((TimeSpan)value).ToString(@"d\.hh\:mm\:ss");
            }
        }

        private StreamingEndpointResource ReturnSelectedStreamingEndpoint()
        {
            if (comboBoxStreamingEndpoint.SelectedItem != null)
            {
                string hostname = ((Item)comboBoxStreamingEndpoint.SelectedItem).Value;
                return _streamingEndpoints.Where(se => se.Data.HostName == hostname).FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        private string ReturnSelectedStreamingEndpointHostname()
        {
            if (comboBoxStreamingEndpoint.SelectedItem != null)
            {
                return ((Item)comboBoxStreamingEndpoint.SelectedItem).Value;
            }
            else
            {
                return null;
            }
        }

        private void LocTreeAddTextEntryToNode(int indexLoc, int indexNode, string text, string value)

        {
            TreeViewLocators.Nodes[indexLoc].Nodes[indexNode].Nodes.Add(new TreeNode(
                     string.Format(text, value)
                     ));
        }

        private void LocTreeAddTextEntryToNode(int indexLoc, int indexNode, string text, DateTime value)

        {
            LocTreeAddTextEntryToNode(indexLoc, indexNode, text, value.ToLocalTime().ToString("G"));
        }

        private void LocTreeAddTextEntryToNode(int indexLoc, int indexNode, string text, DateTime? value)
        {
            if (value != null)
            {
                LocTreeAddTextEntryToNode(indexLoc, indexNode, text, (DateTime)value);
            }
        }

        private async Task BuildLocatorsTreeAsync()
        {
            // LOCATORS TREE
            if (!oktobuildlocator)
            {
                return;
            }

            var SelectedSE = ReturnSelectedStreamingEndpoint();

            if (SelectedSE == null)
            {
                return;
            }

            UriBuilder uriBuilder = new()
            {
                Scheme = checkBoxHttps.Checked ? "https" : "http",
                Host = SelectedSE.Data.HostName
            };

            if (SelectedSE != null)
            {
                Color colornodeRU = Color.Black;

                TreeViewLocators.BeginUpdate();
                TreeViewLocators.Nodes.Clear();
                int indexloc = -1;

                AsyncPageable<MediaAssetStreamingLocator> locators;
                try
                {
                    locators = _asset.GetStreamingLocatorsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                await foreach (MediaAssetStreamingLocator locatorbase in locators)
                {
                    var locator = (await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorbase.Name)).Value;
                    var listPaths = await locator.GetStreamingPathsAsync();
                    //StreamingLocator locator = await _amsClient.AMSclient.strea.StreamingLocators.GetAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, locatorbase.Name);

                    // ListPathsResponse listPaths = await _amsClient.AMSclient.StreamingLocators.ListPathsAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, locator.Name);

                    indexloc++;
                    string locatorstatus = string.Empty;

                    Color colornode = GetLocatorApparence(locator, ref locatorstatus);
                    if (SelectedSE.Data.ResourceState != StreamingEndpointResourceState.Running)
                    {
                        colornode = Color.Red;
                    }

                    TreeNode myLocNode = new(locator.Data.Name)
                    {
                        ForeColor = colornode
                    };

                    TreeViewLocators.Nodes.Add(myLocNode);
                    TreeViewLocators.Nodes[indexloc].Nodes.Add(new TreeNode(AMSExplorer.Properties.Resources.AssetInformation_BuildLocatorsTree_LocatorInformation));

                    LocTreeAddTextEntryToNode(indexloc, 0, "Streaming locator Id: {0}", locator.Data.StreamingLocatorId.ToString());
                    LocTreeAddTextEntryToNode(indexloc, 0, AMSExplorer.Properties.Resources.AssetInformation_BuildLocatorsTree_Name0, locator.Data.Name);
                    LocTreeAddTextEntryToNode(indexloc, 0, "Streaming policy name: {0}", locator.Data.StreamingPolicyName);
                    LocTreeAddTextEntryToNode(indexloc, 0, "Default content key policy name: {0}", locator.Data.DefaultContentKeyPolicyName);
                    LocTreeAddTextEntryToNode(indexloc, 0, "Alt media Id: {0}", locator.Data.AlternativeMediaId);
                    LocTreeAddTextEntryToNode(indexloc, 0, "Created: {0}", locator.Data.CreatedOn?.DateTime.ToString());
                    LocTreeAddTextEntryToNode(indexloc, 0, AMSExplorer.Properties.Resources.AssetInformation_BuildLocatorsTree_StartTime0, locator.Data.StartOn?.DateTime.ToString());
                    LocTreeAddTextEntryToNode(indexloc, 0, AMSExplorer.Properties.Resources.AssetInformation_BuildLocatorsTree_ExpirationDateTime0, locator.Data.EndOn?.DateTime.ToString());
                    LocTreeAddTextEntryToNode(indexloc, 0, "Filters: {0}", string.Join(", ", locator.Data.Filters.ToArray()));

                    int indexn = 1;
                    if (listPaths.Value.StreamingPaths.Count > 0)
                    {
                        string appendExtension = string.Empty;
                        foreach (StreamingPath path in listPaths.Value.StreamingPaths)
                        {
                            TreeViewLocators.Nodes[indexloc].Nodes.Add(new TreeNode(path.StreamingProtocol.ToString()) { ForeColor = colornodeRU });
                            foreach (string p in path.Paths)
                            {
                                appendExtension = string.Empty;
                                if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Dash && !p.EndsWith(Constants.mpd))
                                {
                                    appendExtension = Constants.mpd;
                                }
                                else if (path.StreamingProtocol == StreamingPolicyStreamingProtocol.Hls && !p.EndsWith(Constants.m3u8))
                                {
                                    appendExtension = Constants.m3u8;
                                }
                                uriBuilder.Path = p + appendExtension;
                                TreeViewLocators.Nodes[indexloc].Nodes[indexn].Nodes.Add(new TreeNode(uriBuilder.ToString()) { ForeColor = colornodeRU });
                            }
                            indexn += 1;
                        }
                    }

                    if (listPaths.Value.DownloadPaths.Count > 0)
                    {
                        TreeViewLocators.Nodes[indexloc].Nodes.Add(new TreeNode("Download") { ForeColor = colornodeRU });

                        foreach (string p in listPaths.Value.DownloadPaths)
                        {
                            uriBuilder.Path = p;
                            TreeViewLocators.Nodes[indexloc].Nodes[indexn].Nodes.Add(new TreeNode(uriBuilder.ToString()));
                        }
                    }
                }
                TreeViewLocators.EndUpdate();
            }
        }

        private static Color GetLocatorApparence(StreamingLocatorResource locator, ref string locatorstatus)
        {
            Color colornode;
            switch (AssetTools.GetPublishedStatusForLocator(locator))
            {
                case PublishStatus.PublishedActive:
                    colornode = Color.Black;
                    locatorstatus = "Active";
                    break;
                case PublishStatus.PublishedExpired:
                    colornode = Color.Red;
                    locatorstatus = "Expired";
                    break;
                case PublishStatus.PublishedFuture:
                    colornode = Color.Blue;
                    locatorstatus = "Future";
                    break;
                default:
                    colornode = Color.Black;
                    break;
            }
            return colornode;
        }

        private void DoDisplayFileProperties()
        {
            List<IListBlobItem> SelectedfBlobs = ReturnSelectedBlobs();
            DGFiles.Rows.Clear();

            if (SelectedfBlobs.Count > 0)
            {
                if (SelectedfBlobs.FirstOrDefault() is CloudBlockBlob blob)
                {
                    DGFiles.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name, blob.Name);
                    DGFiles.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_DoDisplayFileProperties_FileSize, AssetTools.FormatByteSize(blob.Properties.Length));
                    DGFiles.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_DoDisplayFileProperties_LastModified, blob.Properties.LastModified != null ? ((DateTimeOffset)blob.Properties.LastModified).ToLocalTime().ToString("G") : null);
                    DGFiles.Rows.Add("Uri", blob.Uri);
                    DGFiles.Rows.Add("MD5", blob.Properties.ContentMD5);
                }
                else if (SelectedfBlobs.FirstOrDefault() is CloudBlobDirectory dir)
                {
                    DGFiles.Rows.Add("Prefix", dir.Prefix);
                    DGFiles.Rows.Add("Uri", dir.Uri);
                    DGFiles.Rows.Add("Size", AssetTools.FormatByteSize(AssetTools.GetSizeBlobDirectory(dir)));
                }
            }
        }

        private async Task DoDisplayTrackPropertiesAsync()
        {
            List<Tuple<string, string>> SelectedTracks = ReturnSelectedTracksNamesAndTypes();

            dGTracks.Rows.Clear();

            if (SelectedTracks.Count > 0)
            {
                var track = (await _asset.GetMediaAssetTrackAsync(SelectedTracks.FirstOrDefault().Item1)).Value;

                dGTracks.Rows.Add("Name", track.Data.Name);

                if (track.Data.Track is TextTrack tt)
                {
                    dGTracks.Rows.Add("Type", texttrack);
                    dGTracks.Rows.Add("Display name", tt.DisplayName);
                    dGTracks.Rows.Add("File name", tt.FileName);
                    dGTracks.Rows.Add("Language code", tt.LanguageCode);
                    dGTracks.Rows.Add("Player visibility", tt.PlayerVisibility);
                    if (tt.HlsSettings != null)
                    {
                        dGTracks.Rows.Add("HLS: Is forced", tt.HlsSettings.IsForced);
                        dGTracks.Rows.Add("HLS: Characteristics", tt.HlsSettings.Characteristics);
                        dGTracks.Rows.Add("HLS: Is default", tt.HlsSettings.IsDefault);
                    }
                }
                else if (track.Data.Track is AudioTrack at)
                {
                    dGTracks.Rows.Add("Type", audiotrack);
                    dGTracks.Rows.Add("Display name", at.DisplayName);
                    dGTracks.Rows.Add("File name", at.FileName);
                    dGTracks.Rows.Add("Language code", at.LanguageCode);
                    dGTracks.Rows.Add("Bitrate", at.BitRate);
                    dGTracks.Rows.Add("DASH role", at.DashRole);
                    dGTracks.Rows.Add("MP4 Track Id", at.Mpeg4TrackId);
                    if (at.HlsSettings != null)
                    {
                        dGTracks.Rows.Add("HLS: Is forced", at.HlsSettings.IsForced);
                        dGTracks.Rows.Add("HLS: Characteristics", at.HlsSettings.Characteristics);
                        dGTracks.Rows.Add("HLS: Is default", at.HlsSettings.IsDefault);
                    }
                }
                else if (track.Data.Track is VideoTrack)
                {
                    dGTracks.Rows.Add("Type", videotrack);
                }
            }
        }


        private void AssetInformation_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        private void ToolStripMenuItemOpenFile_Click(object sender, EventArgs e)
        {
            DoOpenFiles();
        }

        private void DoOpenFiles()
        {
            Telemetry.TrackEvent("AssetInformation DoOpenFilesAsync");

            List<IListBlobItem> SelectedBlobs = ReturnSelectedBlobs();

            try
            {
                _containerSasUrl = GetTemporaryAssetContainerSas();
                // Uri containerSasUrl = new(assetContainerSas.AssetContainerSasUrls.FirstOrDefault());

                foreach (IListBlobItem blob in SelectedBlobs)
                {
                    if (blob is CloudBlockBlob blobtoopen)
                    {
                        //Generate the shared access signature on the blob, setting the constraints directly on the signature.
                        var p = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = blobtoopen.Uri + _containerSasUrl.Query,
                                UseShellExecute = true
                            }
                        };
                        p.Start();
                    }
                }
            }
            catch
            {
                MessageBox.Show("Error when creating the SAS URL for blob");
            }
        }

        private Uri GetTemporaryAssetContainerSas()
        {
            Uri url = null;
            if (_containerSasUrl == null)
            {
                try
                {
                    MediaAssetStorageContainerSasContent content = new()
                    {
                        Permissions = MediaAssetContainerPermission.Read,
                        ExpireOn = DateTime.UtcNow.AddHours(1).ToUniversalTime()
                    };

                    var response = _asset.GetStorageContainerUris(content);
                    url = response.First();
                    _containerSasUrl = url;
                    /*
                        _assetContainerSas = await _amsClient.AMSclient.Assets.ListContainerSasAsync(
                                                                                                         _amsClient.credentialsEntry.ResourceGroup,
                                                                                                         _amsClient.credentialsEntry.AccountName,
                                                                                                         _asset.Name,
                                                                                                         permissions: AssetContainerPermission.Read,
                                                                                                         expiryTime: DateTime.UtcNow.AddHours(1).ToUniversalTime());
                    */

                }

                catch
                {

                }

            }

            return url;
        }

        private async void ToolStripMenuItemDownloadFile_Click(object sender, EventArgs e)
        {
            await DoDownloadBlobsAsync();
        }

        private async Task DoDownloadBlobsAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDownloadBlobsAsync");

            List<IListBlobItem> SelectedBlobs = ReturnSelectedBlobs(false);

            if (SelectedBlobs.Count > 0)
            {
                using (FolderBrowserDialog openFolderDialog = new() { RootFolder = Environment.SpecialFolder.MyVideos })
                {
                    if (openFolderDialog.ShowDialog() == DialogResult.OK)
                    {
                        // let's check if this overwites existing files
                        List<string> listfiles = SelectedBlobs.ToList().Where(f => File.Exists(openFolderDialog.SelectedPath + @"\\" + (f as CloudBlockBlob).Name)).Select(f => openFolderDialog.SelectedPath + @"\\" + (f as CloudBlockBlob).Name).ToList();
                        if (listfiles.Count > 0)
                        {
                            string text;
                            if (listfiles.Count > 1)
                            {
                                text = string.Format(
                                                    AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_TheFollowingFilesAreAlreadyInTheFolderSNN0NNOverwiteTheFiles,
                                                    string.Join("\n", listfiles.Select(f => Path.GetFileName(f)).ToArray())
                                                    );
                            }
                            else
                            {
                                text = string.Format(
                                                     AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_TheFollowingFileIsAlreadyInTheFolderNN0NNOverwiteTheFile,
                                                     string.Join("\n", listfiles.Select(f => Path.GetFileName(f)).ToArray())
                                                     );
                            }

                            if (MessageBox.Show(text, AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_FileSOverwrite, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                            {
                                return;
                            }
                            try
                            {
                                listfiles.ForEach(f => File.Delete(f));
                            }
                            catch
                            {
                                MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_ErrorWhenDeletingFiles, AMSExplorer.Properties.Resources.AMSLogin_buttonExport_Click_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }

                        try
                        {
                            //foreach (var blob in SelectedBlobs)
                            {
                                TransferEntryResponse response = myMainForm.DoGridTransferAddItem(string.Format("Download of blob(s) from asset '{0}'", _asset.Data.Name), TransferType.DownloadToLocal, true);
                                // Start a worker thread that does downloading.
                                //myMainForm.DoDownloadFileFromAsset(myAsset, assetfile, openFolderDialog.FileName, response);
                                await myMainForm.DownloadAssetAsync(_amsClient, _asset.Data.Name, openFolderDialog.SelectedPath, response, DownloadToFolderOption.DoNotCreateSubfolder, false, SelectedBlobs.Select(f => (f as CloudBlockBlob).Name).ToList());
                            }
                            MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_DownloadProcessHasBeenInitiatedSeeTheTransfersTabToCheckTheProgress);

                        }
                        catch
                        {
                            MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDownloadFiles_ErrorWhenDownloadingFileS);
                        }
                    }
                }
            }
        }

        private async void ButtonCopyStats_Click(object sender, EventArgs e)
        {
            await DoDisplayAssetStatsAsync();
        }

        private async Task DoDisplayAssetStatsAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDisplayAssetStatsAsync");

            StringBuilder SB = await AssetTools.GetStatAsync(_asset, _amsClient);
            using (EditorXMLJSON tokenDisplayForm
                = new(AMSExplorer.Properties.Resources.AssetInformation_DoDisplayAssetStats_AssetReport, SB.ToString(), false, ShowSampleMode.None, false))
            {
                tokenDisplayForm.Display();
            }
        }

        private async void ButtonDeleteFile_Click(object sender, EventArgs e)
        {
            await DoDeleteBlobsAsync();
        }

        private async Task DoDeleteBlobsAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDeleteBlobsAsync");

            IEnumerable<CloudBlockBlob> SelectedBlobs = ReturnSelectedBlobs().Where(b => b is CloudBlockBlob).Select(b => (CloudBlockBlob)b);

            if (SelectedBlobs.Any())
            {
                string question = SelectedBlobs.Count() == 1 ? string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoDeleteFiles_DeleteTheFile0, SelectedBlobs.FirstOrDefault().Name) : string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoDeleteFiles_DeleteThese0Files, SelectedBlobs.Count());

                if (System.Windows.Forms.MessageBox.Show(question, AMSExplorer.Properties.Resources.AssetInformation_DoDeleteFiles_FileDeletion, System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    try
                    {
                        Task[] deleteTasks = SelectedBlobs.Select(b => b.DeleteAsync()).ToArray();
                        await Task.WhenAll(deleteTasks);
                    }
                    catch
                    {
                        MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDeleteFiles_ErrorWhenDeletingFileS);
                    }
                    await ListAssetBlobsAsync();
                }
            }
        }

        private async void DeleteFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDeleteBlobsAsync();
            await BuildLocatorsTreeAsync();
        }

        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            DoOpenFiles();
        }

        private async void ButtonDownloadFile_Click(object sender, EventArgs e)
        {
            await DoDownloadBlobsAsync();
        }

        private void listViewFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool bSelect = listViewBlobs.SelectedItems.Count > 0;
            bool bMultiSelect = listViewBlobs.SelectedItems.Count > 1;

            buttonDeleteAll.Enabled = true;
            buttonUpload.Enabled = bSelect;
            DoDisplayFileProperties();
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            await DoDASHIFPlayerAsync();
        }


        private void TreeViewLocators_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (TreeViewLocators.SelectedNode != null)
            {
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    buttonDASH.Enabled = false;
                    buttonAzureMediaPlayer.Enabled = false;
                    buttonOpen.Enabled = false;
                    buttonDel.Enabled = false;

                    switch (TreeViewLocators.SelectedNode.Parent.Text)
                    {
                        case "SmoothStreaming":
                        case "Hls":
                            buttonDASH.Enabled = false;
                            buttonAzureMediaPlayer.Enabled = buttonAdvancedTestPlayer.Enabled = true;
                            buttonOpen.Enabled = false;
                            break;

                        case "Dash":
                            buttonDASH.Enabled = true;
                            buttonAzureMediaPlayer.Enabled = true;
                            buttonAdvancedTestPlayer.Enabled = true;
                            buttonOpen.Enabled = false;
                            break;

                        case "Download":
                            buttonDASH.Enabled = false;
                            buttonAzureMediaPlayer.Enabled = buttonAdvancedTestPlayer.Enabled = (TreeViewLocators.SelectedNode.Text.ToLower().EndsWith(".mp4"));
                            buttonOpen.Enabled = true;
                            break;
                        /*
                    case AssetInfo._prog_down_http_streaming:
                        buttonDASH.Enabled = false;
                        buttonAzureMediaPlayer.Enabled = (TreeViewLocators.SelectedNode.Text.ToLower().EndsWith(".mp4"));
                        buttonOpen.Enabled = !(TreeViewLocators.SelectedNode.Text.ToLower().EndsWith(".ism"));
                        break;
                        */
                        default:
                            break;
                    }
                }
                else
                {
                    buttonDel.Enabled = true; // parent is null, so we can propose to delete the locator
                }
            }
        }

        private async void playbackWithToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoAzureMediaPlayerAsync();
        }

        private async Task DoAzureMediaPlayerAsync(PlayerType playerType = PlayerType.AzureMediaPlayer)
        {
            Telemetry.TrackEvent("AssetInformation DoAzureMediaPlayerAsync");

            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    switch (TreeViewLocators.SelectedNode.Parent.Text)
                    {
                        case "Dash":
                            await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: playerType, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, formatamp: AzureMediaPlayerFormats.Dash, mainForm: myMainForm);

                            break;

                        case "SmoothStreaming":
                            await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: playerType, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, formatamp: AzureMediaPlayerFormats.Smooth, mainForm: myMainForm);
                            break;

                        case "Hls":
                            await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: playerType, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, formatamp: AzureMediaPlayerFormats.HLS, mainForm: myMainForm);
                            break;

                        case "Download":
                            await AssetTools.DoPlayBackWithStreamingEndpointAsync(typeplayer: playerType, path: TreeViewLocators.SelectedNode.Text, DoNotRewriteURL: true, client: _amsClient, formatamp: AzureMediaPlayerFormats.VideoMP4, mainForm: myMainForm);
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private async void Button1_Click(object sender, EventArgs e)
        {
            await DoAzureMediaPlayerAsync();
        }

        private async Task DoDuplicateAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDuplicateAsync");

            IListBlobItem SelectedAssetBlob = ReturnSelectedBlobs().FirstOrDefault();

            if (SelectedAssetBlob != null && SelectedAssetBlob is CloudBlockBlob sourceblob)
            {
                try
                {
                    string newfilename = string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoDuplicate_CopyOf0, sourceblob.Name);
                    if (Program.InputBox(AMSExplorer.Properties.Resources.AssetInformation_DoDuplicate_NewName, AMSExplorer.Properties.Resources.AssetInformation_DoDuplicate_EnterTheNameOfTheNewDuplicateFile, ref newfilename) == DialogResult.OK)
                    {
                        progressBarUpload.Maximum = 100;
                        progressBarUpload.Value = 0;
                        progressBarUpload.Visible = true;
                        buttonClose.Enabled = false;

                        CloudBlockBlob sourceCloudBlob, destinationBlob;

                        sourceCloudBlob = container.GetBlockBlobReference(sourceblob.Name);
                        sourceCloudBlob.FetchAttributes();

                        if (sourceCloudBlob.Properties.Length > 0)
                        {
                            destinationBlob = container.GetBlockBlobReference(newfilename);

                            // Setup the transfer context and track the upload progress
                            SingleTransferContext context = new()
                            {
                                ProgressHandler = new Progress<TransferStatus>((progress) =>
                                {
                                    double percentComplete = 100d * progress.BytesTransferred / sourceCloudBlob.Properties.Length;
                                    progressBarUpload.Value = Convert.ToInt32(percentComplete);
                                })
                            };

                            await TransferManager.CopyAsync(sourceCloudBlob, destinationBlob, CopyMethod.ServiceSideSyncCopy, null, context);
                        }
                    }
                }
                catch
                {
                    MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDuplicate_ErrorWhenDuplicatingThisFile);
                }

                buttonClose.Enabled = true;
                progressBarUpload.Visible = false;

                await ListAssetBlobsAsync();
                await BuildLocatorsTreeAsync();
            }
        }

        private async Task DoUploadAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoUploadAsync");

            using (OpenFileDialog Dialog = new()
            {
                Multiselect = true
            })
            {
                if (Dialog.ShowDialog() == DialogResult.OK)
                {
                    await DoUploadCoreAsync(Dialog.FileNames.ToList());
                }
            }
        }

        private async Task DoUploadCoreAsync(List<string> filenames)
        {
            List<string> listpb = AssetTools.ReturnFilenamesWithProblem(filenames.Select(f => Path.GetFileName(f)).ToList());
            if (listpb.Count > 0)
            {
                MessageBox.Show(AssetTools.FileNameProblemMessage(listpb), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            progressBarUpload.Maximum = 100;
            progressBarUpload.Value = 0;
            progressBarUpload.Visible = true;

            buttonClose.Enabled = false;
            buttonUpload.Enabled = false;

            CloudBlobContainer container = GetRWContainerOfAsset();

            long LengthAllFiles = 0;
            foreach (string file in filenames)
            {
                LengthAllFiles += new System.IO.FileInfo(file).Length;
            }

            // Setup the transfer context and track the upload progress
            SingleTransferContext context = new()
            {
                ProgressHandler = new Progress<TransferStatus>((progress) =>
                {
                    double percentComplete = 100d * progress.BytesTransferred / LengthAllFiles;
                    progressBarUpload.Value = Convert.ToInt32(percentComplete);
                })
            };

            foreach (string file in filenames)
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(Path.GetFileName(file));
                if (file.ToLower().EndsWith(".mp4"))
                {
                    blob.Properties.ContentType = "video/mp4";
                }

                // Upload a local blob
                await TransferManager.UploadAsync(file, blob, null, context);
            }

            progressBarUpload.Visible = false;
            buttonClose.Enabled = true;
            buttonUpload.Enabled = true;
            await ListAssetBlobsAsync();
        }

        private CloudBlobContainer GetRWContainerOfAsset()
        {
            /*
            ListContainerSasInput input = new()
            {
                Permissions = AssetContainerPermission.ReadWrite,
                ExpiryTime = DateTime.Now.AddHours(2).ToUniversalTime()
            };
            */

            MediaAssetStorageContainerSasContent content = new()
            {
                Permissions = MediaAssetContainerPermission.ReadWrite,
                ExpireOn = DateTime.Now.AddHours(2).ToUniversalTime()
            };

            var response = _asset.GetStorageContainerUris(content);
            //AssetContainerSas response = await _amsClient.AMSclient.Assets.ListContainerSasAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, _asset.Name, input.Permissions, input.ExpiryTime);

            //Uri sasUri = new(response.First());
            CloudBlobContainer container = new(response.First());
            return container;
        }


        private async void duplicateFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDuplicateAsync();
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {
            DoOpenFileLocator();
        }

        private void DoOpenFileLocator()
        {
            Telemetry.TrackEvent("AssetInformation DoOpenFileLocator");

            if (TreeViewLocators.SelectedNode != null)
            {
                // Root node's Parent property is null, so do check
                if (TreeViewLocators.SelectedNode.Parent != null)
                {
                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = TreeViewLocators.SelectedNode.Text,
                            UseShellExecute = true
                        }
                    };
                    p.Start();
                }
            }
        }

        private async void buttonDel_Click(object sender, EventArgs e)
        {
            await DoDelLocatorAsync();
        }

        private async Task DoDelLocatorAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDelLocatorAsync");

            if (TreeViewLocators.SelectedNode != null)
            {
                if (TreeViewLocators.SelectedNode.Parent == null)   // user selected the locator title
                {
                    if (System.Windows.Forms.MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDelLocator_AreYouSureThatYouWantToDeleteThisLocator, AMSExplorer.Properties.Resources.AssetInformation_DoDelLocator_LocatorDeletion, System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                    {
                        bool Error = false;
                        try
                        {
                            var locators = await _asset.GetStreamingLocatorsAsync().ToListAsync();
                            var loc = await _amsClient.AMSclient.GetStreamingLocatorAsync(locators[TreeViewLocators.SelectedNode.Index].Name);
                            await loc.Value.DeleteAsync(WaitUntil.Completed);
                            // await _amsClient.AMSclient.StreamingLocators.DeleteAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, locators[TreeViewLocators.SelectedNode.Index].Name);
                        }
                        catch
                        {
                            MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDelLocator_ErrorWhenTryingToDeleteTheLocator);
                            Error = true;
                        }
                        if (!Error)
                        {
                            await BuildLocatorsTreeAsync();
                        }
                    }
                }
            }
        }

        private async void deleteLocatorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDelLocatorAsync();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {

        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await DoUploadAsync();
            await BuildLocatorsTreeAsync();
        }

        private async void uploadASmallFileInTheAssetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoUploadAsync();
            await BuildLocatorsTreeAsync();
        }

        private async void comboBoxStreamingEndpoint_SelectedIndexChanged(object sender, EventArgs e)
        {
            await BuildLocatorsTreeAsync();
        }

        private async void checkBoxHttps_CheckedChanged(object sender, EventArgs e)
        {
            await BuildLocatorsTreeAsync();
        }

        /*
        private void buttonAudioVideoAnalysis_Click(object sender, EventArgs e)
        {
            IEnumerable<AssetFileMetadata> manifestAssetFile = myAsset.GetMetadata();

            IAssetFile metadatafile = myContext.Files.Where(f => f.Name == myAsset.Id.Replace(Constants.AssetIdPrefix, string.Empty) + "_metadata.xml").OrderBy(f => f.LastModified).FirstOrDefault();
            if (metadatafile != null)
            {
                bool Error = false;
                if (tempMetadaLocator == null)
                {
                    try
                    {
                        var locatorTask = Task.Factory.StartNew(() =>
                        {
                            tempMetadaLocator = myContext.Locators.Create(LocatorType.Sas, metadatafile.Asset, AccessPermissions.Read, TimeSpan.FromHours(1));
                        });
                        locatorTask.Wait();
                    }
                    catch
                    {
                        Error = true;
                        MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_buttonAudioVideoAnalysis_Click_ErrorWhenCreatingTheTemporarySASLocatorToTheMetadataFile);
                    }
                }

                try
                {
                    if (!Error)
                    {

                        AssetFileMetadata MyAssetMetada = metadatafile.GetMetadata(tempMetadaLocator);
                    }
                }
                catch
                {
                    MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoOpenFiles_ErrorWhenAccessingTemporarySASLocator);
                }
            }
        }
        */


        private List<IListBlobItem> ReturnSelectedBlobs(bool returnAlsoDirectory = true)
        {
            List<IListBlobItem> Selection = new();

            foreach (int selectedindex in listViewBlobs.SelectedIndices)
            {
                IListBlobItem AF = blobs.Where(af =>
                (af is CloudBlockBlob blob && blob.Name == listViewBlobs.Items[selectedindex].Text)
                ||
                (returnAlsoDirectory && af is CloudBlobDirectory directory && directory.Prefix == listViewBlobs.Items[selectedindex].Text)
                )
                .FirstOrDefault();

                if (AF != null)
                {
                    Selection.Add(AF);
                }
            }
            return Selection;
        }

        private List<Tuple<string, string>> ReturnSelectedTracksNamesAndTypes()
        {
            List<Tuple<string, string>> Selection = new();

            foreach (int selectedindex in listViewTracks.SelectedIndices)
            {
                Selection.Add(Tuple.Create(listViewTracks.Items[selectedindex].Text, listViewTracks.Items[selectedindex].SubItems[1].Text));

            }
            return Selection;
        }

        private void ShowFileMetadata()
        {
            /*
            var SelectedAssetFile = ReturnSelectedBlobs().FirstOrDefault();

            if (SelectedAssetFile != null)
            {
                ILocator locator = GetTemporaryLocator();

                if (locator != null)
                {
                    AssetFileMetadata manifestAssetFile = null;
                    try
                    {
                        // Get the metadata for the asset file.
                        manifestAssetFile = SelectedAssetFile.GetMetadata(locator);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error when accessing metadata." + ex.Message);
                    }

                    if (manifestAssetFile != null)
                    {
                        MetadataInformation form = new MetadataInformation(manifestAssetFile);
                        form.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_ShowFileMetadata_ThereIsNoMetadataForThisFile);
                    }
                }
            }
            */
        }

        private void contextMenuStripDG_MouseClick_1(object sender, MouseEventArgs e)
        {
            ContextMenuStrip contextmenu = (ContextMenuStrip)sender;
            DataGridView DG = (DataGridView)contextmenu.SourceControl;

            if (DG.SelectedCells.Count == 1)
            {
                if (DG.SelectedCells[0].Value != null)
                {
                    try
                    {
                        System.Windows.Forms.Clipboard.SetText(DG.SelectedCells[0].Value.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    System.Windows.Forms.Clipboard.Clear();
                }
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }


        private void contextMenuStripKey_Opening(object sender, CancelEventArgs e)
        {
        }


        private void contextMenuStripFiles_Opening(object sender, CancelEventArgs e)
        {
            var blobs = ReturnSelectedBlobs();

            bool selected = blobs.Count > 0;
            bool bMultiSelect = blobs.Count > 1;

            bool subtitle = blobs.All(b => b is CloudBlob && (((CloudBlob)b).Name.EndsWith(".vtt", StringComparison.CurrentCultureIgnoreCase) || ((CloudBlob)b).Name.EndsWith(".ttml", StringComparison.CurrentCultureIgnoreCase)));

            toolStripMenuItemOpenFile.Enabled = selected;
            editToolStripMenuItem.Enabled = selected && !bMultiSelect;
            toolStripMenuItemDownloadFile.Enabled = selected;
            deleteBlobToolStripMenuItem.Enabled = selected;
            duplicateBlobToolStripMenuItem.Enabled = selected && !bMultiSelect;
            createTextTrackFromThisBlobToolStripMenuItem.Enabled = selected && subtitle;
        }

        private async void filterInfoupdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoFilterInfoAsync();
        }

        private async Task<List<MediaAssetFilterResource>> ReturnSelectedFiltersAsync()
        {
            List<MediaAssetFilterResource> SelectedFilters = new();

            var assetFilters = await _asset.GetMediaAssetFilters().GetAllAsync().ToListAsync();

            foreach (DataGridViewRow Row in dataGridViewFilters.SelectedRows)
            {
                string filterName = Row.Cells[dataGridViewFilters.Columns["Name"].Index].Value.ToString();
                var myfilter = assetFilters.Where(f => f.Data.Name == filterName).FirstOrDefault();
                if (myfilter != null)
                {
                    SelectedFilters.Add(myfilter);
                }
            }
            return SelectedFilters;
        }

        private async Task DoFilterInfoAsync(MediaAssetFilterResource filter = null)
        {
            Telemetry.TrackEvent("AssetInformation DoFilterInfoAsync");

            var filters = await ReturnSelectedFiltersAsync();
            if (filter != null || filters.Count == 1)
            {
                filter ??= filters.FirstOrDefault();
                using (DynManifestFilter form = new(_amsClient, filter, _asset))
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        FilterCreationInfo filtertoupdate;

                        try
                        {
                            filtertoupdate = form.GetFilterInfo;
                            var data = new MediaAssetFilterData
                            {
                                FirstQualityBitrate = filtertoupdate.Firstquality,
                                PresentationTimeRange = filtertoupdate.Presentationtimerange,
                            };
                            foreach (var track in filtertoupdate.Tracks)
                            {
                                data.Tracks.Add(track);
                            }

                            await _asset.GetMediaAssetFilters().CreateOrUpdateAsync(WaitUntil.Completed, filtertoupdate.Name, data);
                            /*
                                 _amsClient.AMSclient.AssetFilters.UpdateAsync(
                                 _amsClient.credentialsEntry.ResourceGroup,
                                 _amsClient.credentialsEntry.AccountName,
                                 _asset.Name,
                                 filter.Name,
                                 new AssetFilter(name: filtertoupdate.Name, presentationTimeRange: filtertoupdate.Presentationtimerange, firstQuality: filtertoupdate.Firstquality, tracks: filtertoupdate.Tracks)
                                 );
                            */

                            myMainForm.TextBoxLogWriteLine(AMSExplorer.Properties.Resources.AssetInformation_DoFilterInfo_AssetFilter0HasBeenUpdated, filtertoupdate.Name);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoFilterInfo_ErrorWhenUpdatingAssetFilter + Constants.endline + Program.GetErrorMessage(e), AMSExplorer.Properties.Resources.AMSLogin_buttonExport_Click_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            myMainForm.TextBoxLogWriteLine(AMSExplorer.Properties.Resources.AssetInformation_DoFilterInfo_ErrorWhenUpdatingAssetFilter0, filter.Data.Name, true);
                            myMainForm.TextBoxLogWriteLine(e);
                        }
                        await DisplayAssetFiltersAsync();
                    }
                }
            }
        }

        private async void createAnAssetFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoCreateAssetFilterAsync();
        }

        private async Task DoCreateAssetFilterAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoCreateAssetFilterAsync");

            using (DynManifestFilter form = new(_amsClient, null, _asset))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    FilterCreationInfo filterinfo = null;
                    try
                    {
                        filterinfo = form.GetFilterInfo;
                        var data = new MediaAssetFilterData
                        {
                            FirstQualityBitrate = filterinfo.Firstquality,
                            PresentationTimeRange = filterinfo.Presentationtimerange,
                        };

                        filterinfo.Tracks.ToList().ForEach(t => data.Tracks.Add(t));

                        await _asset.GetMediaAssetFilters().CreateOrUpdateAsync(
                            WaitUntil.Completed,
                            filterinfo.Name,
                            data
                            );

                        myMainForm.TextBoxLogWriteLine(AMSExplorer.Properties.Resources.AssetInformation_DoCreateAssetFilter_AssetFilter0HasBeenCreated, filterinfo.Name);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoCreateAssetFilter_ErrorWhenCreatingAssetFilter + Constants.endline + Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        myMainForm.TextBoxLogWriteLine(AMSExplorer.Properties.Resources.AssetInformation_DoCreateAssetFilter_ErrorWhenCreatingAssetFilter0, (filterinfo != null && filterinfo.Name != null) ? filterinfo.Name : AMSExplorer.Properties.Resources.AssetInformation_DoCreateAssetFilter_UnknownName, true);
                        myMainForm.TextBoxLogWriteLine(ex);
                        Telemetry.TrackException(ex);
                    }
                    await DisplayAssetFiltersAsync();
                }
            }
        }

        private async void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDeleteAssetFilterAsync();
        }

        private async Task DoDeleteAssetFilterAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDeleteAssetFilterAsync");

            var filters = await ReturnSelectedFiltersAsync();

            try
            {
                await Task.WhenAll(filters.Select
                    (f => f.DeleteAsync(WaitUntil.Completed))
                );
            }
            catch
            {
                MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDeleteAssetFilter_ErrorWhenDeletingAssetFilterS, AMSExplorer.Properties.Resources.AMSLogin_buttonExport_Click_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            await DisplayAssetFiltersAsync();
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            await DoDuplicateFilterAsync();
        }

        private async Task DoDuplicateFilterAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDuplicateFilterAsync");

            var filters = await ReturnSelectedFiltersAsync();
            if (filters.Count == 1)
            {
                var sourcefilter = filters.FirstOrDefault();

                string newfiltername = sourcefilter.Data.Name + AMSExplorer.Properties.Resources.AssetInformation_DoDuplicateFilter_Copy;
                if (Program.InputBox(AMSExplorer.Properties.Resources.AssetInformation_DoDuplicate_NewName, AMSExplorer.Properties.Resources.AssetInformation_DoDuplicateFilter_EnterTheNameOfTheNewDuplicateFilter, ref newfiltername) == DialogResult.OK)
                {


                    try
                    {
                        await _asset.GetMediaAssetFilters().CreateOrUpdateAsync(WaitUntil.Completed, newfiltername, sourcefilter.Data);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDuplicateFilter_ErrorWhenDuplicatingAssetFilter + Constants.endline + Program.GetErrorMessage(e), AMSExplorer.Properties.Resources.AMSLogin_buttonExport_Click_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    await DisplayAssetFiltersAsync();
                }
            }
        }

        private async Task DoDeleteAllBlobsAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDeleteAllBlobsAsync");

            try
            {
                string question = "Delete all blobs ?";
                if (System.Windows.Forms.MessageBox.Show(question, AMSExplorer.Properties.Resources.AssetInformation_DoDeleteFiles_FileDeletion, System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    CloudBlockBlob[] ArrayBlobs = blobs.Where(b => b is CloudBlockBlob).Select(b => (CloudBlockBlob)b).ToArray();
                    List<Task> deleteTasks = new();

                    for (int i = 0; i < ArrayBlobs.Length; i++)
                    {
                        deleteTasks.Add(ArrayBlobs[i].DeleteAsync());
                    }
                    await Task.WhenAll(deleteTasks);
                }
            }
            catch
            {
                MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoDeleteAllFiles_ErrorWhenDeletingTheFiles);
            }
            await ListAssetBlobsAsync();
            await BuildLocatorsTreeAsync();
        }

        private async void duplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDuplicateFilterAsync();
        }

        private async void dataGridViewFilters_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            await DoFilterInfoAsync();
        }


        private async void button1_Click_3(object sender, EventArgs e)
        {
            await DoFilterInfoAsync();
        }

        private async void button2_Click_1(object sender, EventArgs e)
        {
            await DoCreateAssetFilterAsync();
        }

        private async void buttonDeleteFilter_Click(object sender, EventArgs e)
        {
            await DoDeleteAssetFilterAsync();
        }

        private async void button1_Click_4(object sender, EventArgs e)
        {
            await DoPlayWithFilterAsync();
        }

        private async Task DoPlayWithFilterAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoPlayWithFilterAsync");
            var selFilters = await ReturnSelectedFiltersAsync();
            await myMainForm.DoPlaySelectedAssetsOrProgramsWithPlayerAsync(PlayerType.AzureMediaPlayer, new List<AssetLiveOutputEntry>() { new() { Asset = _asset } }, selFilters.FirstOrDefault().Data.Name);
        }

        private async void playWithThisFilterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoPlayWithFilterAsync();
        }

        private async void buttonDeleteAll_Click(object sender, EventArgs e)
        {
            await DoDeleteAllBlobsAsync();
        }


        /// <summary>
        /// 
        /// </summary>
        private async void DoEditFile()
        {
            Telemetry.TrackEvent("AssetInformation DoEditFile");

            List<IListBlobItem> SelectedBlobs = ReturnSelectedBlobs();

            if (SelectedBlobs.Count == 1 && SelectedBlobs.FirstOrDefault() != null && SelectedBlobs.FirstOrDefault() is CloudBlockBlob blobtoedit)
            {
                if (blobtoedit.Properties.Length > 500 * 1000)
                {
                    MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_FileIsToLargeToEditItOnline);
                    return;
                }

                try
                {
                    progressBarUpload.Maximum = 100;
                    progressBarUpload.Visible = true;

                    string contentstring = await blobtoedit.DownloadTextAsync();

                    progressBarUpload.Visible = false;

                    using (EditorXMLJSON editform = new(string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_OnlineEditOf0, blobtoedit.Name), contentstring, true))
                    {
                        if (editform.Display() == DialogResult.OK)
                        { // OK

                            progressBarUpload.Visible = true;
                            buttonClose.Enabled = false;
                            await blobtoedit.UploadTextAsync(editform.TextData);

                            progressBarUpload.Visible = false;
                            buttonClose.Enabled = true;
                            await ListAssetBlobsAsync();
                        }
                    }
                }

                catch
                {
                    MessageBox.Show(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_ErrorWhenAccessingEditingAssetFile);
                }

            }
        }

        private void toolStripMenuItemFilesCopyClipboard_Click(object sender, EventArgs e)
        {

        }


        private void SeeClearKey(string key)
        {
            using (EditorXMLJSON editform = new(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_Value, key.ToString(), false))
                editform.Display();
        }

        private void dataGridViewKeys_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView senderGrid = (DataGridView)sender;

            if (e.RowIndex >= 0 && senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewButtonCell)
            {
                SeeClearKey(senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag.ToString());
            }

        }

        private void dataGridViewAutPolOption_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView senderGrid = (DataGridView)sender;
            if (e.RowIndex >= 0 && senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex] is DataGridViewButtonCell)
            {
                SeeValueInEditor(senderGrid.Rows[e.RowIndex].Cells[0].Value.ToString(), senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag.ToString());
            }
        }

        private void SeeValueInEditor(string dataname, string key)
        {
            using (EditorXMLJSON editform = new(dataname, key, false))
                editform.Display();
        }

        private async void buttonGenerateManifest_Click(object sender, EventArgs e)
        {
            await DoGenerateServerManifestAsync();
        }

        private async Task DoGenerateServerManifestAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoGenerateServerManifestAsync");
            try
            {
                GeneratedServerManifest smildata = await ServerManifestUtils.LoadAndUpdateManifestTemplateAsync(container);

                using (
                EditorXMLJSON editform = new(string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_OnlineEditOf0, smildata.FileName), smildata.Content, true, ShowSampleMode.None, true,
                    AMSExplorer.Properties.Resources.AssetInformation_DoGenerateManifest_PleaseCheckCarefullyTheContentOfTheGeneratedManifestAsTheToolMakesGuesses))
                {
                    if (editform.Display() == DialogResult.OK)
                    { // OK

                        string tempPath = System.IO.Path.GetTempPath();
                        string filePath = Path.Combine(tempPath, smildata.FileName);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        XDocument doc = XDocument.Parse(editform.TextData);
                        doc.Save(filePath);

                        progressBarUpload.Visible = true;
                        buttonClose.Enabled = false;

                        CloudBlobContainer container = GetRWContainerOfAsset();

                        CloudBlockBlob blob = container.GetBlockBlobReference(Path.GetFileName(filePath));

                        // await Task.Factory.StartNew(() => blob.UploadFromFile(filePath));
                        await TransferManager.UploadAsync(filePath, blob);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                }
            }
            catch
            {

            }
            progressBarUpload.Visible = false;
            buttonClose.Enabled = true;
            await ListAssetBlobsAsync();
            await BuildLocatorsTreeAsync();
        }

        private async void tabPageBlobs_Enter(object sender, EventArgs e)
        {
            await ListAssetBlobsAsync();
        }

        private async void tabPage6_Enter(object sender, EventArgs e)
        {
            await DisplayAssetFiltersAsync();
        }

        private async void tabPage3_Enter(object sender, EventArgs e)
        {
            await BuildLocatorsTreeAsync();
        }

        private async void dataGridViewFilters_CellDoubleClickAsync(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                var filter = await _asset.GetMediaAssetFilterAsync(dataGridViewFilters.Rows[e.RowIndex].Cells[dataGridViewFilters.Columns["Name"].Index].Value.ToString());
                await DoFilterInfoAsync(filter);
            }
        }

        private async void tabPagePolicy_Enter(object sender, EventArgs e)
        {
            await FillLocatorComboInPolicyTabAsync();
        }

        private async Task FillLocatorComboInPolicyTabAsync()
        {
            comboBoxPolicyLocators.Items.Clear();
            comboBoxPolicyLocators.BeginUpdate();


            AsyncPageable<MediaAssetStreamingLocator> locators;
            //IList<AssetStreamingLocator> locators = null;
            try
            {
                locators = _asset.GetStreamingLocatorsAsync();
                //locators = (await _amsClient.AMSclient.Assets.ListStreamingLocatorsAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, _asset.Name)).StreamingLocators;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await foreach (var l in locators)
            {
                comboBoxPolicyLocators.Items.Add(new Item(l.Name, l.Name));
            }

            if (comboBoxPolicyLocators.Items.Count > 0)
            {
                comboBoxPolicyLocators.SelectedIndex = 0;
            }

            comboBoxPolicyLocators.EndUpdate();
        }


        private async Task DisplayStreamingPolicyAndContentKeyPolicyOfLocatorAsync(string locatorName)
        {
            if (locatorName == null)
            {
                textBoxStreamingPolicyOfLocator.Text = string.Empty;
                return;
            }

            var locator = await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorName);

            //var policy = await _amsClient.AMSclient.GetStreamingPolicyAsync(locator.Value.Data.StreamingPolicyName);

            // new code (REST)
            var streamingPolicy = await _restClient.GetStreamingPolicyAsync(locator.Value.Data.StreamingPolicyName);
            textBoxStreamingPolicyOfLocator.Text = Program.AnalyzeAndIndentXMLJSON(streamingPolicy);

            /*
            string policyJson = JsonConvert.SerializeObject(policy.Value.Data, Newtonsoft.Json.Formatting.Indented);
            textBoxStreamingPolicyOfLocator.Text = policyJson;
            */

            await DisplayContentKeyPolicyOfStreamingPolicyAsync(locator.Value.Data.StreamingPolicyName);

        }

        private async Task DisplayContentKeyPolicyOfLocatorAsync(string locatorName)
        {
            if (locatorName == null)
            {
                textBoxContentKeyPolicyOfLocator.Text = string.Empty;
                return;
            }
            var locator = await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorName);

            await DisplayContentKeyPolicyAsync(locator.Value.Data.DefaultContentKeyPolicyName, textBoxContentKeyPolicyOfLocator);
        }

        private async Task DisplayContentKeyPolicyOfStreamingPolicyAsync(string streamingPolicyName)
        {
            if (string.IsNullOrEmpty(streamingPolicyName))
            {
                textBoxContentKeyPolicyOfStreamingPolicy.Text = string.Empty;
                return;
            }
            var spol = await _amsClient.AMSclient.GetStreamingPolicyAsync(streamingPolicyName);

            await DisplayContentKeyPolicyAsync(spol.Value.Data.DefaultContentKeyPolicyName, textBoxContentKeyPolicyOfLocator);
        }

        private async Task DisplayContentKeyPolicyAsync(string contentKeyPolicyName, TextBox myTextBox)
        {
            if (string.IsNullOrEmpty(contentKeyPolicyName))
            {
                myTextBox.Text = string.Empty;
                return;
            }

            //ContentKeyPolicyResource policy = await _amsClient.AMSclient.GetContentKeyPolicyAsync(contentKeyPolicyName);
            //string policyJson = JsonConvert.SerializeObject(policy.Data, Newtonsoft.Json.Formatting.Indented);

            // new code (REST)
            var existingCk = await _restClient.GetContentKeyPolicyAsync(contentKeyPolicyName);
            myTextBox.Text = Program.AnalyzeAndIndentXMLJSON(existingCk);

            //myTextBox.Text = policyJson;
        }


        private async void comboBoxPolicyLocators_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxStreamingPolicyOfLocator.Text = string.Empty;

            if (comboBoxPolicyLocators.SelectedItem != null)
            {
                string locatorName = (comboBoxPolicyLocators.SelectedItem as Item).Value;
                await DisplayStreamingPolicyAndContentKeyPolicyOfLocatorAsync(locatorName);
                await DisplayContentKeyPolicyOfLocatorAsync(locatorName);
                await FillComboDRMKeysAsync(locatorName);
                await FillComboContentKeyOptionsAsync(locatorName);
            }
        }

        private async Task FillComboContentKeyOptionsAsync(string locatorName)
        {
            comboBoxOptions.Items.Clear();
            var locator = (await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorName)).Value;
            var spolicy = (await _amsClient.AMSclient.GetStreamingPolicyAsync(locator.Data.StreamingPolicyName)).Value;

            // let's find active key policy
            ContentKeyPolicyResource ckpolicy = null;
            try
            {
                if (!string.IsNullOrEmpty(locator.Data.DefaultContentKeyPolicyName))
                {
                    ckpolicy = await _amsClient.AMSclient.GetContentKeyPolicyAsync(locator.Data.DefaultContentKeyPolicyName);
                }
                else if (!string.IsNullOrEmpty(spolicy.Data.DefaultContentKeyPolicyName))
                {
                    ckpolicy = await _amsClient.AMSclient.GetContentKeyPolicyAsync(spolicy.Data.DefaultContentKeyPolicyName);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == ((int)System.Net.HttpStatusCode.NotFound))
            {
            }

            if (ckpolicy == null || (ckpolicy.Data.Options.First().Restriction.GetType() != typeof(ContentKeyPolicyTokenRestriction)))
            {
                comboBoxOptions.Enabled = false;
                buttonGetDRMToken.Enabled = false;
                return;
            };

            comboBoxOptions.Enabled = true;
            buttonGetDRMToken.Enabled = true;

            foreach (ContentKeyPolicyOption o in ckpolicy.Data.Options)
            {
                if (o.Restriction.GetType() == typeof(ContentKeyPolicyTokenRestriction))
                {
                    comboBoxOptions.Items.Add(new Item(string.Format("{0} ({1}) {2}", o.Name, o.PolicyOptionId, o.Configuration.GetType().Name), o.PolicyOptionId.ToString()));
                }
            }

            if (ckpolicy.Data.Options.Count > 0)
            {
                comboBoxOptions.SelectedIndex = 0;
            }
        }

        private async Task FillComboDRMKeysAsync(string locatorName)
        {
            comboBoxKeys.Items.Clear();
            var loc = await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorName);
            //ListContentKeysResponse response = await _amsClient.AMSclient.StreamingLocators.ListContentKeysAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, locatorName);
            contentKeysForCurrentLocator = loc.Value.GetContentKeysAsync().ToListAsync().Result;
            contentKeysForCurrentLocator.ForEach(k => comboBoxKeys.Items.Add(new Item(k.LabelReferenceInStreamingPolicy, k.Id.ToString())));
            if (contentKeysForCurrentLocator.Count > 0)
            {
                comboBoxKeys.SelectedIndex = 0;
            }
        }

        private void comboBoxKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxKeys.SelectedItem != null)
            {
                string keyId = (comboBoxKeys.SelectedItem as Item).Value;
                DisplayKeyInfo(keyId);
            }
        }

        private void DisplayKeyInfo(string keyId)
        {
            StreamingLocatorContentKey key = contentKeysForCurrentLocator.Where(k => k.Id == Guid.Parse(keyId)).FirstOrDefault();
            if (key == null)
            {
                return;
            }

            dataGridViewKeys.Rows.Clear();
            dataGridViewKeys.Rows.Add("LabelReferenceInStreamingPolicy", key.LabelReferenceInStreamingPolicy);
            dataGridViewKeys.Rows.Add("Id", key.Id);
            dataGridViewKeys.Rows.Add("PolicyName", key.PolicyName);
            dataGridViewKeys.Rows.Add("Type", key.KeyType);

            string tracksJson = JsonConvert.SerializeObject(key.Tracks, Newtonsoft.Json.Formatting.Indented);
            int i = dataGridViewKeys.Rows.Add("Tracks", "Details");
            DataGridViewButtonCell btn2 = new();
            dataGridViewKeys.Rows[i].Cells[1] = btn2;
            dataGridViewKeys.Rows[i].Cells[1].Value = "See details";
            dataGridViewKeys.Rows[i].Cells[1].Tag = tracksJson;

            i = dataGridViewKeys.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_DoDisplayKeyPropertiesAndAutOptions_ClearKeyValue, AMSExplorer.Properties.Resources.AssetInformation_DoDisplayKeyPropertiesAndAutOptions_SeeClearKey);
            DataGridViewButtonCell btn = new();
            dataGridViewKeys.Rows[i].Cells[1] = btn;
            dataGridViewKeys.Rows[i].Cells[1].Value = AMSExplorer.Properties.Resources.AssetInformation_DoDisplayKeyPropertiesAndAutOptions_SeeClearKey2;
            dataGridViewKeys.Rows[i].Cells[1].Tag = key.Value;
        }

        private void dataGridViewKeys_CellContentClick_1(object sender, DataGridViewCellEventArgs e)
        {
            DataGridView senderGrid = (DataGridView)sender;

            if (e.RowIndex >= 0 && senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].GetType() == typeof(DataGridViewButtonCell))
            {

                SeeValueInEditor(senderGrid.Rows[e.RowIndex].Cells[0].Value.ToString(), senderGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag.ToString());
            }
        }

        private async Task GetDRMTestTokenAsync()
        {
            Telemetry.TrackEvent("AssetInformation GetDRMTestTokenAsync");

            if (comboBoxPolicyLocators.SelectedItem == null)
            {
                return;
            }

            StringBuilder sbuilder = new();

            string locatorName = (comboBoxPolicyLocators.SelectedItem as Item).Value;

            var locator = (await _amsClient.AMSclient.GetStreamingLocatorAsync(locatorName)).Value;
            var spolicy = (await _amsClient.AMSclient.GetStreamingPolicyAsync(locator.Data.StreamingPolicyName)).Value;

            // let's find active key policy
            //ContentKeyPolicyResource ckpolicy = null;
            ContentKeyPolicyProperties cKproperties = null;
            try
            {
                if (!string.IsNullOrEmpty(locator.Data.DefaultContentKeyPolicyName))
                {
                    var ckpolicy1 = await _amsClient.AMSclient.GetContentKeyPolicyAsync(locator.Data.DefaultContentKeyPolicyName);
                    cKproperties = await ckpolicy1.Value.GetPolicyPropertiesWithSecretsAsync();
                }
                else if (!string.IsNullOrEmpty(spolicy.Data.DefaultContentKeyPolicyName))
                {
                    var ckpolicy2 = await _amsClient.AMSclient.GetContentKeyPolicyAsync(spolicy.Data.DefaultContentKeyPolicyName);
                    cKproperties = await ckpolicy2.Value.GetPolicyPropertiesWithSecretsAsync();
                }
            }
            catch (RequestFailedException ex) when (ex.Status == ((int)System.Net.HttpStatusCode.NotFound))
            {
                return;
            }

            Guid optionId = Guid.Parse((comboBoxOptions.SelectedItem as Item).Value);

            ContentKeyPolicyTokenRestriction ckrestriction = (ContentKeyPolicyTokenRestriction)cKproperties.Options.Where(o => o.PolicyOptionId == optionId).FirstOrDefault()?.Restriction;

            // we support only symmetric key
            if (ckrestriction.PrimaryVerificationKey.GetType() != typeof(ContentKeyPolicySymmetricTokenKey))
            {
                MessageBox.Show("From the asset information dialog box, AMSE can only generate a test token key when the signing key in the policy is symmetric.", "Not a symmetric key", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ContentKeyPolicySymmetricTokenKey SymKey = (ContentKeyPolicySymmetricTokenKey)ckrestriction.PrimaryVerificationKey;

            string keyIdentifier = (comboBoxKeys.SelectedItem as Item).Value;

            using (DRM_GenerateToken formTokenProperties = new())
            {
                formTokenProperties.ShowDialog();
                if (formTokenProperties.DialogResult == DialogResult.OK)
                {
                    Microsoft.IdentityModel.Tokens.SymmetricSecurityKey tokenSigningKey = new(SymKey.KeyValue);

                    Microsoft.IdentityModel.Tokens.SigningCredentials signingcredentials = new(tokenSigningKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.Sha256Digest);

                    List<Claim> claims = new();

                    foreach (ContentKeyPolicyTokenClaim claim in ckrestriction.RequiredClaims)
                    {
                        if (claim.ClaimType == "urn:microsoft:azure:mediaservices:contentkeyidentifier")
                        {
                            //claims.Add(new Claim(ContentKeyPolicyTokenClaim.ContentKeyIdentifierClaim.ClaimType, keyIdentifier));
                            claims.Add(new Claim("urn:microsoft:azure:mediaservices:contentkeyidentifier", keyIdentifier));
                        }
                        else
                        {
                            claims.Add(new Claim(claim.ClaimType, claim.ClaimValue));
                        }
                    }

                    if (formTokenProperties.TokenUse != null)
                    {
                        claims.Add(new Claim("urn:microsoft:azure:mediaservices:maxuses", ((int)formTokenProperties.TokenUse).ToString()));
                    }

                    JwtSecurityToken token = new(
                                                                issuer: ckrestriction.Issuer,
                                                                audience: ckrestriction.Audience,
                                                                claims: claims.Count > 0 ? claims : null,
                                                                notBefore: DateTime.Now.AddMinutes(-5),
                                                                expires: DateTime.Now.AddMinutes(formTokenProperties.TokenDuration),
                                                                signingCredentials: signingcredentials
                                                                );


                    JwtSecurityTokenHandler handler = new();

                    sbuilder.Append("Bearer " + handler.WriteToken(token));
                }
            }

            using (EditorXMLJSON displayResult = new("Test token", sbuilder.ToString(), false, ShowSampleMode.None, false))
                displayResult.ShowDialog();
        }

        private void AssetInformation_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            // for controls which are not using the default font
            // DpiUtils.UpdatedSizeFontAfterDPIChange(new List<Control> { labelAssetNameTitle, textBoxStreamingPolicyOfLocator, textBoxContentKeyPolicyOfStreamingPolicy, textBoxContentKeyPolicyOfLocator, contextMenuStripLocators, contextMenuStripDG, contextMenuStripBlobs, contextMenuStripKey, contextMenuStripFilter }, e, this);
            //DpiUtils.UpdatedSizeFontAfterDPIChangeV8(new List<Control> { textBoxStreamingPolicyOfLocator, textBoxContentKeyPolicyOfStreamingPolicy, textBoxContentKeyPolicyOfLocator }, e, this);
        }

        private async void Button1_Click_2(object sender, EventArgs e)
        {
            await DoAzureMediaPlayerAsync(PlayerType.AdvancedTestPlayer);
        }

        private void ListViewFiles_DragEnter(object sender, DragEventArgs e)
        {
            // If the data is a file display the copy cursor. 
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private async void ListViewFiles_DragDrop(object sender, DragEventArgs e)
        {
            await DoDragAndDropUploadAsync(e);
        }

        private async Task DoDragAndDropUploadAsync(DragEventArgs e)
        {
            // Handle FileDrop data. 
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Assign the file names to a string array, in  
                // case the user has selected multiple files. 
                string[] objects = (string[])e.Data.GetData(DataFormats.FileDrop);

                List<string> files = objects.Where(f => !Directory.Exists(f)).ToList();
                await DoUploadCoreAsync(files);
            }
        }

        private async void CheckBoxListBlobsDirectories_CheckedChanged(object sender, EventArgs e)
        {
            await ListAssetBlobsAsync();
        }

        private void EditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoEditFile();
        }

        private async void buttonGetDRMToken_Click_1(object sender, EventArgs e)
        {
            await GetDRMTestTokenAsync();
        }

        private async void buttonGenerateClientManifest_Click(object sender, EventArgs e)
        {
            await DoGenerateClientManifestAsync();
        }

        private async Task DoGenerateClientManifestAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoGenerateClientManifestAsync");

            // let's read the smooth manifest

            StreamingLocatorResource tempStreamingLocator = null;
            // let's create a clear streaming locator
            if (MessageBox.Show("A temporary clear locator is going to be created to read the client manifest from the streaming endpoint. It will be deleted just after.", "Locator creation", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
            {
                try
                {
                    tempStreamingLocator = Task.Run(() => AssetTools.CreateTemporaryOnDemandLocatorAsync(_asset, _amsClient)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // let's try to read client manifest
            XDocument manifest = null;
            try
            {
                manifest = await AssetTools.TryToGetClientManifestContentUsingStreamingLocatorAsync(_asset, _amsClient, tempStreamingLocator?.Data.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                // let's delete the temp clear locator
                try
                {
                    await AssetTools.DeleteStreamingLocatorAsync(_amsClient, tempStreamingLocator.Data.Name);
                }
                catch
                {

                }
            }

            try
            {
                string clientManifestName = _serverManifestName + "c";

                using (EditorXMLJSON editform = new(
                                                                    string.Format(AMSExplorer.Properties.Resources.AssetInformation_DoEditFile_OnlineEditOf0, clientManifestName),
                                                                    manifest.ToString(),
                                                                    true,
                                                                    ShowSampleMode.None,
                                                                    true,
                                                                    null
                                                                   ))
                {
                    if (editform.Display() == DialogResult.OK)
                    { // OK

                        string tempPath = System.IO.Path.GetTempPath();
                        string filePath = Path.Combine(tempPath, clientManifestName);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        XDocument doc = XDocument.Parse(editform.TextData);
                        doc.Save(filePath);

                        progressBarUpload.Visible = true;
                        buttonClose.Enabled = false;

                        CloudBlobContainer container = GetRWContainerOfAsset();
                        CloudBlockBlob blob = container.GetBlockBlobReference(Path.GetFileName(filePath));

                        // await Task.Factory.StartNew(() => blob.UploadFromFile(filePath));
                        await TransferManager.UploadAsync(filePath, blob);

                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        // let's edit the server manifest file to add reference to client manifest
                        CloudBlockBlob blobServerManifest = container.GetBlockBlobReference(_serverManifestName);
                        string contentServerManifest = await blobServerManifest.DownloadTextAsync();
                        contentServerManifest = XmlManifestUtils.AddIsmcToIsm(contentServerManifest, clientManifestName);
                        await blobServerManifest.UploadTextAsync(contentServerManifest);

                        MessageBox.Show($"The client manifest '{clientManifestName}' has been created and the server manifest '{_serverManifestName}' has been updated to reference it.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBarUpload.Visible = false;
                buttonClose.Enabled = true;
                await ListAssetBlobsAsync();
                await BuildLocatorsTreeAsync();
            }
        }

        private void AssetInformation_Shown(object sender, EventArgs e)
        {
            Telemetry.TrackPageView(this.Name);
            Telemetry.TrackPageView(this.Name + " tab " + tabControl1.SelectedTab.Name);
        }

        private void tabControl1_Selected(object sender, TabControlEventArgs e)
        {
            TabControl tabcontrol = (TabControl)sender;
            Telemetry.TrackPageView(this.Name + " tab " + tabcontrol.SelectedTab.Name);
        }

        private async void tabPage8_Enter(object sender, EventArgs e)
        {
            await ListAssetTracksAsync();

        }

        private async Task DoDeleteTracksAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoDeleteTracksAsync");

            var SelectedTracks = ReturnSelectedTracksNamesAndTypes();

            if (SelectedTracks.Any())
            {
                string question = SelectedTracks.Count() == 1 ? string.Format("Delete the '{0}' track ?", SelectedTracks.FirstOrDefault().Item1) : string.Format("Delete these {0} tracks ?", SelectedTracks.Count());

                if (System.Windows.Forms.MessageBox.Show(question, "Tracks deletion", System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
                {
                    Cursor = Cursors.WaitCursor;
                    try
                    {
                        var tracksOp = SelectedTracks.Select(t => _asset.GetMediaAssetTrackAsync(t.Item1)).ToArray();
                        await Task.WhenAll(tracksOp);
                        var deleteTasks = tracksOp.Select(t => t.Result.Value.DeleteAsync(WaitUntil.Completed)).ToArray();

                        //                        Task[] deleteTasks = SelectedTracks.Select(b => _amsClient.AMSclient.Tracks.DeleteAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, _asset.Name, b.Item1)).ToArray();
                        //Task[] deleteTasks = SelectedTracks.Select(b =>  _asset.GetMediaAssetTracks(). .tra.trac .Tracks.DeleteAsync(_amsClient.credentialsEntry.ResourceGroup, _amsClient.credentialsEntry.AccountName, _asset.Name, b.Item1)).ToArray();

                        await Task.WhenAll(deleteTasks);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error when deleting track(s).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    await ListAssetTracksAsync();
                    Cursor = Cursors.Arrow;
                }
            }
        }

        private async Task DoCreateTexttrackFromBlobAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoCreateTexttrackFromBlobAsync");

            var SelectedBlobs = ReturnSelectedBlobs();

            if (SelectedBlobs.Any())
            {
                foreach (var blob in SelectedBlobs)
                {
                    if (blob is CloudBlockBlob bl)
                    {
                        var texttracksnames = await ReturnTracksNamesAsync(typeof(TextTrack));

                        // let's find a name not used
                        int i = 1;
                        string trackname;

                        do
                        {
                            trackname = "text" + i;
                            i++;

                        } while (texttracksnames.Contains(trackname));

                        AssetInfoTextTrackCreation form = new(bl.Name, trackname);
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            Cursor = Cursors.WaitCursor;
                            try
                            {
                                var data = new MediaAssetTrackData
                                {
                                    Track = new TextTrack()
                                    {
                                        // TODO2023
                                        // this properties should be written but v1 of the SDK has this constraint
                                        // LanguageCode = form.LanguageCode,
                                        DisplayName = form.LanguageDisplayName,
                                        FileName = bl.Name,
                                        PlayerVisibility = form.VisibleInPlayer ? PlayerVisibility.Visible : PlayerVisibility.Hidden,
                                        HlsSettings = new HlsSettings()
                                        {
                                            IsDefault = form.HLSDefaultTrack,
                                            IsForced = form.HLSSetForced,
                                            Characteristics = form.HLSAccessibilityCharacteristics
                                        }
                                    }
                                };
                                var track = await _asset.GetMediaAssetTracks().CreateOrUpdateAsync(WaitUntil.Completed, form.TrackName, data);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error when creating text track(s)." + Constants.endline + Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            Cursor = Cursors.Arrow;
                        }
                    }
                }
                await ListAssetBlobsAsync();
            }
        }


        private async Task DoCreateAudiotrackFromBlobAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoCreateAudiotrackFromBlobAsync");

            var SelectedBlobs = ReturnSelectedBlobs();

            if (SelectedBlobs.Any())
            {
                foreach (var blob in SelectedBlobs)
                {
                    if (blob is CloudBlockBlob bl)
                    {
                        var audiotracksnames = await ReturnTracksNamesAsync(typeof(AudioTrack));

                        // let's find a name not used
                        int i = 1;
                        string trackname;

                        do
                        {
                            trackname = "audio" + i;
                            i++;

                        } while (audiotracksnames.Contains(trackname));

                        AssetInfoAudioTrackCreation form = new(bl.Name, trackname);
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            Cursor = Cursors.WaitCursor;
                            try
                            {
                                var data = new MediaAssetTrackData
                                {
                                    Track = new AudioTrack()
                                    {
                                        // TODO2023
                                        // this properties should be written but v1 of the SDK has this constraint
                                        //LanguageCode = form.LanguageCode,
                                        DisplayName = form.LanguageDisplayName,
                                        FileName = bl.Name,
                                        DashRole = form.DashRole,
                                        HlsSettings = new HlsSettings()
                                        {
                                            IsDefault = form.HLSDefaultTrack,
                                            Characteristics = form.HLSIsDescriptiveAudio ? "public.accessibility.describes-video" : null
                                        },
                                        Mpeg4TrackId = form.Mp4TrackId
                                    }
                                };
                                var track = await _asset.GetMediaAssetTracks().CreateOrUpdateAsync(WaitUntil.Completed, form.TrackName, data);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Error when creating audio track(s)." + Constants.endline + Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            Cursor = Cursors.Arrow;
                        }
                    }
                }
                await ListAssetBlobsAsync();
            }
        }

        private async void listViewTracks_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool bSelect = listViewTracks.SelectedItems.Count > 0;
            bool bMultiSelect = listViewTracks.SelectedItems.Count > 1;
            deleteTrackToolStripMenuItem.Enabled = bSelect;
            await DoDisplayTrackPropertiesAsync();
        }

        private async void deleteTrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoDeleteTracksAsync();
        }

        private async void createTextTrackFromThisBlobToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoCreateTexttrackFromBlobAsync();
        }

        private void contextMenuStripTracks_Opening(object sender, CancelEventArgs e)
        {
            var names = ReturnSelectedTracksNamesAndTypes();
            bool subtitle = names.All(b => b.Item2 == texttrack);
            bool audio = names.All(b => b.Item2 == audiotrack);
            deleteTrackToolStripMenuItem.Enabled = editSettingsToolStripMenuItem.Enabled = subtitle || audio;
        }

        private void addTrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("To add a track, go the asset blobs tab. From there, upload a new blob, select it and create a track with the contextual menu.", "Add a track", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void createAnAudioTrackFromThisBlobToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoCreateAudiotrackFromBlobAsync();
        }

        private async void editSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DoEditTrackSettingsAsync();
        }

        private async Task DoEditTrackSettingsAsync()
        {
            Telemetry.TrackEvent("AssetInformation DoEditTrackSettingsAsync");

            var SelectedTracks = ReturnSelectedTracksNamesAndTypes();

            if (SelectedTracks.Any())
            {
                var returnedTrack = (await _asset.GetMediaAssetTrackAsync(SelectedTracks.FirstOrDefault().Item1)).Value;
                var tbase = returnedTrack.Data;
                if (tbase.Track is AudioTrack at)
                {
                    AssetInfoAudioTrackCreation form = new(at.FileName, tbase.Name, at);
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        Cursor = Cursors.WaitCursor;
                        try
                        {
                            at.DashRole = form.DashRole;
                            at.DisplayName = form.LanguageDisplayName;
                            at.HlsSettings = new HlsSettings()
                            {
                                IsDefault = form.HLSDefaultTrack,
                                Characteristics = form.HLSIsDescriptiveAudio ? "public.accessibility.describes-video" : null
                            };
                            var track = await _asset.GetMediaAssetTracks().CreateOrUpdateAsync(WaitUntil.Completed, tbase.Name, new MediaAssetTrackData { Track = at });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error when updating audio track." + Constants.endline + Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        Cursor = Cursors.Arrow;
                    }
                }
                else if (tbase.Track is TextTrack tt)
                {
                    AssetInfoTextTrackCreation form = new(tt.FileName, tbase.Name, tt);
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        Cursor = Cursors.WaitCursor;
                        try
                        {
                            tt.DisplayName = form.LanguageDisplayName;
                            tt.PlayerVisibility = form.VisibleInPlayer ? PlayerVisibility.Visible : PlayerVisibility.Hidden;
                            tt.HlsSettings = new HlsSettings()
                            {
                                IsDefault = form.HLSDefaultTrack,
                                IsForced = form.HLSSetForced,
                                Characteristics = form.HLSAccessibilityCharacteristics
                            };
                            var track = await _asset.GetMediaAssetTracks().CreateOrUpdateAsync(WaitUntil.Completed, tbase.Name, new MediaAssetTrackData { Track = tt });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error when updating text track." + Constants.endline + Program.GetErrorMessage(ex), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        Cursor = Cursors.Arrow;
                    }
                }
                await DoDisplayTrackPropertiesAsync();
            }
        }
    }
}