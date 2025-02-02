﻿//----------------------------------------------------------------------------------------------
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


using AMSExplorer.Utils.JobInfo;
using Azure.ResourceManager.Media;
using Azure.ResourceManager.Media.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AMSExplorer
{
    public partial class JobInformation : Form
    {
        private readonly MediaJobResource _job;
        private readonly AMSClientV3 _amsClient;
        private readonly Mainform _mainform;
        private readonly JobExtension _jobExt;
        public IEnumerable<StreamingEndpointResource> MyStreamingEndpoints;

        public JobInformation(Mainform mainform, AMSClientV3 client, JobExtension jobExt)
        {
            InitializeComponent();
            Icon = Bitmaps.Azure_Explorer_ico;
            _amsClient = client;
            _mainform = mainform;
            _jobExt = jobExt;
            _job = jobExt.Job;

        }

        private void contextMenuStrip_MouseClick(object sender, MouseEventArgs e)
        {
            ContextMenuStrip contextmenu = (ContextMenuStrip)sender;
            DataGridView DG = (DataGridView)contextmenu.SourceControl;

            if (DG.SelectedCells.Count == 1)
            {
                if (DG.SelectedCells[0].Value != null)
                {
                    System.Windows.Forms.Clipboard.SetText(DG.SelectedCells[0].Value.ToString());
                }
                else
                {
                    System.Windows.Forms.Clipboard.Clear();
                }
            }
        }

        private async void buttonCopyStats_Click(object sender, EventArgs e)
        {
            await DoJobStatAsync();
        }

        public async Task DoJobStatAsync()
        {
            Telemetry.TrackEvent("JobInformation DoJobStatAsync");
            StringBuilder SB = await JobTools.GetStatAsync(_jobExt, _amsClient);
            var tokenDisplayForm = new EditorXMLJSON(AMSExplorer.Properties.Resources.JobInformation_DoJobStats_JobReport, SB.ToString(), false, ShowSampleMode.None, false);
            tokenDisplayForm.Display();
        }

        private void JobInformation_Load(object sender, EventArgs e)
        {
            // DpiUtils.InitPerMonitorDpi(this);

            labelJobNameTitle.Text += _job.Data.Name;

            DGJob.ColumnCount = 2;
            DGOutputs.ColumnCount = 2;
            DGOutputs.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;

            dataGridInput.ColumnCount = 2;
            dataGridInput.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;

            DGErrors.ColumnCount = 3;
            DGErrors.Columns[0].HeaderText = AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_Task;
            DGErrors.Columns[1].HeaderText = AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_ErrorDetail;
            DGErrors.Columns[2].HeaderText = AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_Code;

            DGJob.Columns[0].DefaultCellStyle.BackColor = Color.Gainsboro;
            DGJob.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name, _job.Data.Name);
            DGJob.Rows.Add("Description", _job.Data.Description);
            DGJob.Rows.Add("Id", _job.Id);
            DGJob.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_State, _job.Data.State);
            DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_Priority, _job.Data.Priority);
            //DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_OverallProgress, MyJob.GetOverallProgress());

            if (_job.Data.StartOn.HasValue) DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_StartTime, _job.Data.StartOn?.DateTime.ToLocalTime().ToString("G"));
            if (_job.Data.EndOn.HasValue) DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_EndTime, _job.Data.EndOn?.DateTime.ToLocalTime().ToString("G"));

            if ((_job.Data.StartOn.HasValue) && (_job.Data.EndOn.HasValue))
            {
                DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_JobDuration, _job.Data.EndOn?.Subtract((DateTimeOffset)_job.Data.StartOn));
            }

            // DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_CPUDuration, MyJob.RunningDuration);
            DGJob.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Created, _job.Data.CreatedOn?.DateTime.ToLocalTime().ToString("G"));
            DGJob.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_LastModified, _job.Data.LastModifiedOn?.DateTime.ToLocalTime().ToString("G"));
            // DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_TemplateId, MyJob.TemplateId);

            /*

            TaskSizeAndPrice jobSizePrice = JobInfo.CalculateJobSizeAndPrice(MyJob);
            if ((jobSizePrice.InputSize != -1) && (jobSizePrice.OutputSize != -1))
            {
                DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_InputSize, AssetInfo.FormatByteSize(jobSizePrice.InputSize));
                DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_OutputSize, AssetInfo.FormatByteSize(jobSizePrice.OutputSize));
                DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_ProcessedSize, AssetInfo.FormatByteSize(jobSizePrice.InputSize + jobSizePrice.OutputSize));
                //if (jobSizePrice.Price != -1) DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_EstimatedPrice, string.Format("{0} {1:0.00}", Properties.Settings.Default.Currency, jobSizePrice.Price));
            }
            else
            {
                DGJob.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_InputOutputSize, AMSExplorer.Properties.Resources.JobInformation_JobInformation_Load_UndefinedTaskDidNotFinishOrOneOfTheAssetsHasBeenDeleted);
            }
            */


            // input assets


            if (_job.Data.Input is MediaJobInputSequence dd) // multiple inputs
            {
                int index1 = 1;
                foreach (var input in dd.Inputs)
                {
                    string inputLabel = "input #" + index1;
                    listBoxInput.Items.Add(inputLabel);
                    index1++;
                }
            }
            else
            {
                string inputLabel = "input";
                listBoxInput.Items.Add(inputLabel);
            }

            listBoxInput.SelectedIndex = 0;

            // output assets

            bool boutoutsinjobs = (_job.Data.Outputs.Count > 0);

            int index = 1;
            if (boutoutsinjobs)
            {
                foreach (var output in _job.Data.Outputs)
                {
                    // listBoxTasks.Items.Add(output..Name ?? Constants.stringNull);
                    string outputLabel = "output #" + index;
                    listBoxOutputs.Items.Add(outputLabel);

                    if (output.Error != null && output.Error.Details != null)
                    {
                        for (int i = 0; i < output.Error.Details.Count; i++)
                        {
                            DGErrors.Rows.Add(outputLabel, output.Error.Details[i].Message, output.Error.Details[i].Code);
                        }
                    }
                    index++;
                }
                listBoxOutputs.SelectedIndex = 0;
            }

        }


        private async void listBoxOutputs_SelectedIndexChanged(object sender, EventArgs e)
        {
            var output = _job.Data.Outputs.Skip(listBoxOutputs.SelectedIndex).Take(1).FirstOrDefault();

            DGOutputs.Rows.Clear();

            //  DGTasks.Rows.Add(AMSExplorer.Properties.Resources.AssetInformation_AssetInformation_Load_Name, task.Name);

            /*
            int i = DGOutputs.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_listBoxTasks_SelectedIndexChanged_Configuration, "");
            DataGridViewButtonCell btn = new DataGridViewButtonCell();
            DGOutputs.Rows[i].Cells[1] = btn;
            DGOutputs.Rows[i].Cells[1].Value = AMSExplorer.Properties.Resources.JobInformation_listBoxTasks_SelectedIndexChanged_SeeClearValue;
            DGOutputs.Rows[i].Cells[1].Tag =  task.GetClearConfiguration();
            */

            /*
            i = DGOutputs.Rows.Add(AMSExplorer.Properties.Resources.JobInformation_listBoxTasks_SelectedIndexChanged_Body, "");
            btn = new DataGridViewButtonCell();
            DGOutputs.Rows[i].Cells[1] = btn;
            DGOutputs.Rows[i].Cells[1].Value = AMSExplorer.Properties.Resources.AssetInformation_DoDisplayAuthorizationPolicyOption_SeeValue;
            DGOutputs.Rows[i].Cells[1].Tag = task.TaskBody;
            */

            DGOutputs.Rows.Add("Progress", output.Progress);
            DGOutputs.Rows.Add("State", output.State);

            if (output is MediaJobOutputAsset outputA)
            {
                DGOutputs.Rows.Add("Asset name", outputA.AssetName);
                DGOutputs.Rows.Add("Asset type", (await AssetTools.GetAssetTypeAsync(outputA, _amsClient))?.Type);
            }


            if (output.Error != null && output.Error.Details != null)
            {
                for (int j = 0; j < output.Error.Details.Count; j++)
                {
                    DGOutputs.Rows.Add("Error", output.Error.Details[j].Code + ": " + output.Error.Details[j].Message);
                }
            }
        }

        private void DGTasks_CellContentClick(object sender, DataGridViewCellEventArgs e)
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

        private void assetInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DisplayAssetInfo(true);
        }

        private void DisplayAssetInfo(bool input)
        {

            string assetName = null;

            if (input)
            {
                if (_job.Data.Input is MediaJobInputAsset inputAsset)
                {
                    assetName = inputAsset.AssetName;
                }

            }
            else  // output
            {
                int index = listBoxOutputs.SelectedIndices[0];

                if (_job.Data.Outputs[index] is MediaJobOutputAsset outputAsset)
                {
                    assetName = outputAsset.AssetName;
                }
            }

            if (assetName != null)
            {

                MediaAssetResource asset = Task.Run(() =>
                                       _amsClient.GetAssetAsync(assetName))
                                        .GetAwaiter().GetResult();

                using (AssetInformation form = new(
                    _mainform,
                    _amsClient,
                    asset,
                    MyStreamingEndpoints // we want to keep the same sorting
                    )
               )
                {
                    DialogResult dialogResult = form.ShowDialog(this);
                }
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            DisplayAssetInfo(false);
        }

        private async void listBoxInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            dataGridInput.Rows.Clear();

            if (_job.Data.Input is MediaJobInputAsset inputA)
            {
                dataGridInput.Rows.Add("Input type", "asset");
                dataGridInput.Rows.Add("Asset name", inputA.AssetName);

                var inputAasset = await _amsClient.AMSclient.GetMediaAssetAsync(inputA.AssetName);

                dataGridInput.Rows.Add("Asset type", (await AssetTools.GetAssetTypeAsync(inputAasset, _amsClient))?.Type);
                if (inputA.Start != null && inputA.Start is AbsoluteClipTime startA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time Start", startA.Time.ToString());
                }
                if (inputA.End != null && inputA.End is AbsoluteClipTime endA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time End", endA.Time.ToString());
                }
                dataGridInput.Rows.Add("Label", inputA.Label);
                dataGridInput.Rows.Add("Files", string.Join(Constants.endline, inputA.Files));
            }
            else if (_job.Data.Input is MediaJobInputSequence inputS)
            {
                MediaJobInputClip clip = inputS.Inputs[listBoxInput.SelectedIndex];

                if (clip is MediaJobInputAsset iAsset)
                {
                    dataGridInput.Rows.Add("Input type", "asset");
                    dataGridInput.Rows.Add("Asset name", iAsset.AssetName);
                }
                else
                {
                    dataGridInput.Rows.Add("Input type", "clip");
                }

                if (clip.Start != null && clip.Start is AbsoluteClipTime startA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time Start", startA.Time.ToString());
                }
                if (clip.End != null && clip.End is AbsoluteClipTime endA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time End", endA.Time.ToString());
                }
                dataGridInput.Rows.Add("Label", clip.Label);
                dataGridInput.Rows.Add("Files", string.Join(Constants.endline, clip.Files));
                foreach (var idef in clip.InputDefinitions)
                {
                    dataGridInput.Rows.Add("Input definition", idef.ToString());
                }
            }
            else if (_job.Data.Input is MediaJobInputHttp inputH)
            {
                dataGridInput.Rows.Add("Input type", "https");
                dataGridInput.Rows.Add("Base Url", inputH.BaseUri);
                if (inputH.Start != null && inputH.Start is AbsoluteClipTime startA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time Start", startA.Time.ToString());
                }
                if (inputH.End != null && inputH.End is AbsoluteClipTime endA)
                {
                    dataGridInput.Rows.Add("Absolute Clip Time End", endA.Time.ToString());
                }
                dataGridInput.Rows.Add("Label", inputH.Label);
                dataGridInput.Rows.Add("Files", string.Join(Constants.endline, inputH.Files));
            }
        }

        private void JobInformation_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            // for controls which are not using the default font
            // DpiUtils.UpdatedSizeFontAfterDPIChange(new List<Control> { labelJobNameTitle, contextMenuStrip, contextMenuStripInputAsset, contextMenuStripOutputAsset }, e, this);
        }

        private void JobInformation_Shown(object sender, EventArgs e)
        {
            Telemetry.TrackPageView(this.Name);
        }
    }
}
