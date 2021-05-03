﻿//----------------------------------------------------------------------------------------------
//    Copyright 2021 Microsoft Corporation
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


using AMSExplorer.AMSLogin;
using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.Rest.Azure.Authentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace AMSExplorer
{
    public partial class AmsLogin : Form
    {
        private ListCredentialsRPv3 CredentialList = new ListCredentialsRPv3();

        public string accountName;

        private CredentialsEntryV3 LoginInfo;
        private AzureEnvironment environment;

        public AMSClientV3 AmsClient { get; private set; }

        public AmsLogin()
        {
            Font = new Font("Segoe UI", 9);
            InitializeComponent();
            Icon = Bitmaps.Azure_Explorer_ico;
        }

        private void AMSLogin_Load(object sender, EventArgs e)
        {
            //Properties.Settings.Default.LoginListRPv3JSON = "";
            //// DpiUtils.InitPerMonitorDpi(this);

            // Add a dummy column     
            ColumnHeader header = new ColumnHeader
            {
                Text = "",
                Name = "col1"
            };
            listViewAccounts.Columns.Add(header);
            // Then
            listViewAccounts.Scrollable = true;
            listViewAccounts.View = System.Windows.Forms.View.Details;


            if (!string.IsNullOrWhiteSpace(Properties.Settings.Default.LoginListRPv3JSON))
            {
                // JSon deserialize
                CredentialList = (ListCredentialsRPv3)JsonConvert.DeserializeObject(Properties.Settings.Default.LoginListRPv3JSON, typeof(ListCredentialsRPv3));


                // Display accounts in the list
                CredentialList.MediaServicesAccounts.ForEach(c =>
                    AddItemToListviewAccounts(c)
                );
            }

            buttonExport.Enabled = (listViewAccounts.Items.Count > 0);

            accountmgtlink.Links.Add(new LinkLabel.Link(0, accountmgtlink.Text.Length, Constants.LinkAMSCreateAccount));
            linkLabelAADAut.Links.Add(new LinkLabel.Link(0, linkLabelAADAut.Text.Length, Constants.LinkAMSAADAut));

            // version
            labelVersion.Text = string.Format(labelVersion.Text, Assembly.GetExecutingAssembly().GetName().Version);

            DoEnableManualFields(false);
        }

        private void AddItemToListviewAccounts(CredentialsEntryV3 c)
        {
            ListViewItem item = listViewAccounts.Items.Add(c.MediaService.Name);
            listViewAccounts.Items[item.Index].ForeColor = Color.Black;
            listViewAccounts.Items[item.Index].ToolTipText = null;
        }

        private void ButtonDeleteAccount_Click(object sender, EventArgs e)
        {
            // let's remove the selected items

            for (int i = listViewAccounts.Items.Count - 1; i >= 0; i--)
            {
                if (listViewAccounts.Items[i].Selected)
                {
                    listViewAccounts.Items[i].Remove();
                    CredentialList.MediaServicesAccounts.RemoveAt(i);
                }
            }

            SaveCredentialsToSettings();
        }

        private async void ButtonLogin_Click(object sender, EventArgs e)
        {
            if (listViewAccounts.SelectedIndices.Count != 1)
            {
                return;
            }
            // code when used from pick-up
            LoginInfo = CredentialList.MediaServicesAccounts[listViewAccounts.SelectedIndices[0]];

            if (LoginInfo == null)
            {
                MessageBox.Show("Error", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                //MessageBox.Show(string.Format("The {0} cannot be empty.", labelE1.Text), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            AmsClient = new AMSClientV3(LoginInfo.Environment, LoginInfo.AzureSubscriptionId, LoginInfo, this);

            AzureMediaServicesClient response;
            try
            {
                response = await AmsClient.ConnectAndGetNewClientV3Async(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            if (response == null)
            {
                return;
            }

            // let's save the credentials (SP) They may be updated by the user when connecting
            CredentialList.MediaServicesAccounts[listViewAccounts.SelectedIndices[0]] = AmsClient.credentialsEntry;
            SaveCredentialsToSettings();

            try
            {   // let's refresh storage accounts
                AmsClient.credentialsEntry.MediaService.StorageAccounts = (await AmsClient.AMSclient.Mediaservices.GetAsync(AmsClient.credentialsEntry.ResourceGroup, AmsClient.credentialsEntry.AccountName)).StorageAccounts;
                Cursor = Cursors.Default;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Program.GetErrorMessage(ex), "Login error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Cursor = Cursors.Default;
                return;
            }

            DialogResult = DialogResult.OK;  // form will close with OK result
                                             // else --> form won't close...
        }


        private void ListViewAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            buttonLogin.Enabled = (listViewAccounts.SelectedIndices.Count == 1); // no selected item, so login button not active
            buttonDeleteAccountEntry.Enabled = (listViewAccounts.SelectedIndices.Count > 0); // no selected item, so login button not active
            buttonExport.Enabled = (listViewAccounts.Items.Count > 0);

            if (listViewAccounts.SelectedIndices.Count > 0) // one selected
            {
                LoginInfo = CredentialList.MediaServicesAccounts[listViewAccounts.SelectedIndices[0]];

                textBoxDescription.Text = LoginInfo.Description;
                textBoxAMSResourceId.Text = LoginInfo.MediaService.Id;
                textBoxAADtenantId.Text = LoginInfo.AadTenantId;

                DoEnableManualFields(false);
                groupBoxAADAutMode.Visible = true;

                radioButtonAADInteractive.Checked = !LoginInfo.UseSPAuth;
                radioButtonAADServicePrincipal.Checked = LoginInfo.UseSPAuth;
            }
        }


        private void DoEnableManualFields(bool enable)
        {
            textBoxAADtenantId.Enabled =
            textBoxAMSResourceId.Enabled =
                                    enable;
        }


        private void ButtonExport_Click(object sender, EventArgs e)
        {
            bool exportAll = true;
            bool exportSPSecrets = false;
            ExportSettings form = new ExportSettings();

            // There are more than one entry and one has been selected. 
            form.radioButtonAllEntries.Enabled = CredentialList.MediaServicesAccounts.Count > 1 && listViewAccounts.SelectedIndices.Count > 0;
            form.checkBoxIncludeSPSecrets.Checked = exportSPSecrets;


            if (form.ShowDialog() == DialogResult.OK)
            {
                exportAll = form.radioButtonAllEntries.Checked;
                exportSPSecrets = form.checkBoxIncludeSPSecrets.Checked;

                PropertyRenameAndIgnoreSerializerContractResolver jsonResolver = new PropertyRenameAndIgnoreSerializerContractResolver();
                List<string> properties = new List<string> { "EncryptedADSPClientSecret" };
                if (!exportSPSecrets)
                {
                    properties.Add("ClearADSPClientSecret");
                }

                jsonResolver.IgnoreProperty(typeof(CredentialsEntryV3), properties.ToArray()); // let's not export encrypted secret and may be clear secret

                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    ContractResolver = jsonResolver
                };

                DialogResult diares = saveFileDialog1.ShowDialog();
                if (diares == DialogResult.OK)
                {
                    try
                    {
                        if (exportAll)
                        {
                            System.IO.File.WriteAllText(saveFileDialog1.FileName, JsonConvert.SerializeObject(CredentialList, settings));
                        }
                        else
                        {
                            ListCredentialsRPv3 copyEntry = new ListCredentialsRPv3();

                            for (int i = 0; i < listViewAccounts.SelectedIndices.Count; i++)
                            {
                                copyEntry.MediaServicesAccounts.Add(CredentialList.MediaServicesAccounts[listViewAccounts.SelectedIndices[i]]);
                            }

                            System.IO.File.WriteAllText(saveFileDialog1.FileName, JsonConvert.SerializeObject(copyEntry, settings));
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, AMSExplorer.Properties.Resources.AMSLogin_buttonExport_Click_Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ButtonImportAll_Click(object sender, EventArgs e)
        {
            bool mergesentries = false;

            if (CredentialList.MediaServicesAccounts.Count > 0) // There are entries. Let's ask if user want to delete them or merge
            {
                if (System.Windows.Forms.MessageBox.Show(AMSExplorer.Properties.Resources.AMSLogin_buttonImportAll_Click_ThereAreCurrentEntriesInTheListNDoYouWantReplaceThemWithTheNewOnesOrDoAMergeNNSelectYesToReplaceThemNoToMergeThem, AMSExplorer.Properties.Resources.AMSLogin_buttonImportAll_Click_ImportAndReplace, System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
                {
                    mergesentries = true;
                }
            }

            DialogResult diares = openFileDialog1.ShowDialog();
            if (diares == DialogResult.OK)
            {
                if (Path.GetExtension(openFileDialog1.FileName).ToLower() == ".json")
                {
                    string json = System.IO.File.ReadAllText(openFileDialog1.FileName);

                    if (!mergesentries)
                    {
                        CredentialList.MediaServicesAccounts.Clear();
                        // let's purge entries if user does not want to keep them
                    }

                    ListCredentialsRPv3 ImportedCredentialList = null;
                    try
                    {
                        ImportedCredentialList = (ListCredentialsRPv3)JsonConvert.DeserializeObject(json, typeof(ListCredentialsRPv3));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error when importing json file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (ImportedCredentialList.Version < (new ListCredentialsRPv3()).Version)
                    {
                        MessageBox.Show("This file was created with an older version of AMSE. Import is not possible.", "Wrong version", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    CredentialList.MediaServicesAccounts.AddRange(ImportedCredentialList.MediaServicesAccounts);

                    listViewAccounts.Items.Clear();
                    CredentialList.MediaServicesAccounts.ForEach(c => AddItemToListviewAccounts(c));
                    buttonExport.Enabled = (listViewAccounts.Items.Count > 0);

                    // let's save the list of credentials in settings
                    SaveCredentialsToSettings();
                }
            }
        }

        private void SaveCredentialsToSettings()
        {
            PropertyRenameAndIgnoreSerializerContractResolver jsonResolver = new PropertyRenameAndIgnoreSerializerContractResolver();
            jsonResolver.IgnoreProperty(typeof(CredentialsEntryV3), "ClearADSPClientSecret"); // let's not save the clear SP secret
            JsonSerializerSettings settings = new JsonSerializerSettings() { ContractResolver = jsonResolver };
            Properties.Settings.Default.LoginListRPv3JSON = JsonConvert.SerializeObject(CredentialList, settings);
            Program.SaveAndProtectUserConfig();
        }

        private void Accountmgtlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = e.Link.LinkData as string,
                UseShellExecute = true
            };
            p.Start();
        }

        private async void AMSLogin_ShownAsync(object sender, EventArgs e)
        {

            //await Task.Run(() => Program.CheckAMSEVersionAsync()).ConfigureAwait(false); //let not wait for this task - no need
            ScaleListViewColumns(listViewAccounts);

            await Program.CheckAMSEVersionAsync();
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void CheckTextBox(object sender)
        {
            TextBox tb = (TextBox)sender;

            if (string.IsNullOrEmpty(tb.Text))
            {
                errorProvider1.SetError(tb, AMSExplorer.Properties.Resources.AMSLogin_CheckTextBox_ThisFieldIsMandatory);
            }
            else
            {
                errorProvider1.SetError(tb, string.Empty);
            }
        }


        private void ListBoxAcounts_DoubleClick(object sender, EventArgs e)
        {
            // Proceed to log in to the selected account in the listbox
            ButtonLogin_Click(sender, e);
        }


        private void TextBoxAADtenant_Validating(object sender, CancelEventArgs e)
        {
            CheckTextBox(sender);
        }

        private void textBoxRestAPIEndpoint_Validating(object sender, CancelEventArgs e)
        {
            TextBox tb = (TextBox)sender;

            if (string.IsNullOrEmpty(tb.Text))
            {
                errorProvider1.SetError(tb, AMSExplorer.Properties.Resources.AMSLogin_CheckTextBox_ThisFieldIsMandatory);
            }
            else
            {
                bool Error = false;
                try
                {
                    Uri url = new Uri(tb.Text);
                }
                catch
                {
                    Error = true;
                }

                if (Error)
                {
                    errorProvider1.SetError(tb, "Please insert a valid URL");

                }
                else
                {
                    errorProvider1.SetError(tb, string.Empty);

                }
            }
        }


        private async void buttonPickupAccount_Click(object sender, EventArgs e)
        {
            AddAMSAccount1 addaccount1 = new AddAMSAccount1();
            if (addaccount1.ShowDialog() == DialogResult.OK)
            {
                if (addaccount1.SelectedMode == AddAccountMode.BrowseSubscriptions)
                {
                    Cursor = Cursors.WaitCursor;
                    Prompt prompt = addaccount1.SelectUser ? Prompt.ForceLogin : Prompt.SelectAccount;

                    //string[] scopes = { "User.Read" };
                    environment = addaccount1.GetEnvironment();
                    var scopes = new[] { environment.AADSettings.TokenAudience.ToString() + "/user_impersonation" };


                    IPublicClientApplication app = PublicClientApplicationBuilder.Create(environment.ClientApplicationId)
                        .WithAuthority(environment.AADSettings.AuthenticationEndpoint.ToString() + "organizations")
                        .WithRedirectUri("http://localhost")
                        .Build();



                    /*
                    AuthenticationContext authContext = new AuthenticationContext(
                                                                // authority:  environment.Authority,
                                                                authority: environment.AADSettings.AuthenticationEndpoint.ToString() + "common",
                                                             validateAuthority: true    
                    );
                    */

                    AuthenticationResult accessToken = null;
                    var accounts = await app.GetAccountsAsync();
                    try
                    {

                        accessToken = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                        /*
                        accessToken = await authContext.AcquireTokenAsync(
                                                                             resource: environment.AADSettings.TokenAudience.ToString(),
                                                                             clientId: environment.ClientApplicationId,
                                                                             redirectUri: new Uri("urn:ietf:wg:oauth:2.0:oob"),
                                                                             parameters: new PlatformParameters()
                                                                             //parameters: new PlatformParameters(addaccount1.SelectUser ? PromptBehavior.SelectAccount : PromptBehavior.Auto)
                                                                             );
                        */

                    }
#pragma warning disable CS0168 // Variable is declared but never used
                    catch (MsalUiRequiredException ex)
#pragma warning restore CS0168 // Variable is declared but never used
                    {
                        try
                        {
                            accessToken = await app.AcquireTokenInteractive(scopes)
                                .WithPrompt(addaccount1.SelectUser ? Prompt.ForceLogin : Prompt.SelectAccount)
                                .WithCustomWebUi(new EmbeddedBrowserCustomWebUI(this))
                                .ExecuteAsync();
                        }
                        catch (MsalException)
                        {

                        }
                    }
                    catch (Exception ex)
                    {
                        Cursor = Cursors.Default;
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    TokenCredentials credentials = new TokenCredentials(accessToken.AccessToken, "Bearer");

                    SubscriptionClient subscriptionClient = new SubscriptionClient(environment.ArmEndpoint, credentials);

                    // Subcriptions listing
                    List<Subscription> subscriptions = new List<Subscription>();
                    IPage<Subscription> subscriptionsPage = subscriptionClient.Subscriptions.List();
                    while (subscriptionsPage != null)
                    {
                        subscriptions.AddRange(subscriptionsPage);
                        if (subscriptionsPage.NextPageLink != null)
                        {
                            subscriptionsPage = subscriptionClient.Subscriptions.ListNext(subscriptionsPage.NextPageLink);
                        }
                        else
                        {
                            subscriptionsPage = null;
                        }
                    }

                    // Tenants listing
                    List<TenantIdDescription> tenants = new List<TenantIdDescription>();
                    IPage<TenantIdDescription> tenantsPage = subscriptionClient.Tenants.List();
                    while (tenantsPage != null)
                    {
                        tenants.AddRange(tenantsPage);
                        if (tenantsPage.NextPageLink != null)
                        {
                            tenantsPage = subscriptionClient.Tenants.ListNext(tenantsPage.NextPageLink);
                        }
                        else
                        {
                            tenantsPage = null;
                        }
                    }

                    /*
                    // Tenants browsing
                    myTenants tenants = new myTenants();
                    string URL = environment.ArmEndpoint + "tenants?api-version=2020-01-01";

                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Remove("Authorization");
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken.AccessToken);
                    HttpResponseMessage response = await client.GetAsync(URL);
                    if (response.IsSuccessStatusCode)
                    {
                        string str = await response.Content.ReadAsStringAsync();
                        tenants = (myTenants)JsonConvert.DeserializeObject(str, typeof(myTenants));
                    }
                    */
                    Cursor = Cursors.Default;

                    AddAMSAccount2Browse addaccount2 = new AddAMSAccount2Browse(credentials, subscriptions, environment, tenants, prompt);

                    //  AddAMSAccount2Browse addaccount2 = new AddAMSAccount2Browse(credentials, subscriptions, environment, tenants/*, new PlatformParameters(addaccount1.SelectUser ? PromptBehavior.SelectAccount : PromptBehavior.Auto)*/);

                    if (addaccount2.ShowDialog() == DialogResult.OK)
                    {
                        // Getting Media Services accounts...
                        AzureMediaServicesClient mediaServicesClient = new AzureMediaServicesClient(environment.ArmEndpoint, credentials);

                        CredentialsEntryV3 entry = new CredentialsEntryV3(addaccount2.selectedAccount,
                            environment,
                            addaccount1.SelectUser,
                            false,
                            addaccount2.selectedTenantId,
                            false
                            );
                        CredentialList.MediaServicesAccounts.Add(entry);
                        AddItemToListviewAccounts(entry);

                        SaveCredentialsToSettings();
                    }
                    else
                    {
                        return;
                    }
                }


                // Get info from the Portal or Azure CLI JSON
                else if (addaccount1.SelectedMode == AddAccountMode.FromAzureCliOrPortalJson)
                {
                    string example = @"{
  ""AadClientId"": ""00000000-0000-0000-0000-000000000000"",
  ""AadSecret"": ""00000000-0000-0000-0000-000000000000"",
  ""AadTenantId"": ""00000000-0000-0000-0000-000000000000"",
  ""AccountName"": ""amsaccount"",
  ""ResourceGroup"": ""amsResourceGroup"",
  ""SubscriptionId"": ""00000000-0000-0000-0000-000000000000"",
  ""ArmAadAudience"": ""https://management.core.windows.net/"",
  ""ArmEndpoint"": ""https://management.azure.com/"",
  ""AadEndpoint"": ""https://login.microsoftonline.com""
}";
                    EditorXMLJSON form = new EditorXMLJSON("Enter the JSON output of the Azure Portal or Azure Cli Service Principal creation", example, true, ShowSampleMode.None, true, "The Service Principal secret is stored encrypted in the application settings.");

                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        JsonFromAzureCliOrPortal json;
                        try
                        {
                            json = (JsonFromAzureCliOrPortal)JsonConvert.DeserializeObject(form.TextData, typeof(JsonFromAzureCliOrPortal));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Error reading the json", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        string resourceId = string.Format("/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Media/mediaservices/{2}", json.SubscriptionId, json.ResourceGroup, json.AccountName);
                        string AADtenantId = json.AadTenantId;

                        // environment
                        ActiveDirectoryServiceSettings aadSettings = new ActiveDirectoryServiceSettings()
                        {
                            AuthenticationEndpoint = json.AadEndpoint ?? addaccount1.GetEnvironment().AADSettings.AuthenticationEndpoint,
                            TokenAudience = json.ArmAadAudience,
                            ValidateAuthority = true
                        };

                        AzureEnvironment env = new AzureEnvironment(AzureEnvType.Custom) { AADSettings = aadSettings, ArmEndpoint = json.ArmEndpoint };

                        CredentialsEntryV3 entry = new CredentialsEntryV3(
                                                        // new MediaService(resourceId, json.AccountName, null, null, json.Location ?? json.Region),
                                                        new MediaService(null, resourceId, json.AccountName),

                                                        env,
                                                        true,
                                                        true,
                                                        AADtenantId,
                                                        false,
                                                        json.AadClientId,
                                                        json.AadSecret
                                                        );

                        CredentialList.MediaServicesAccounts.Add(entry);
                        AddItemToListviewAccounts(entry);

                        SaveCredentialsToSettings();
                    }
                    else
                    {
                        return;
                    }

                }
                else if (addaccount1.SelectedMode == AddAccountMode.ManualEntry)
                {
                    AddAMSAccount2Manual form = new AddAMSAccount2Manual();
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        string accountnamecc = form.textBoxAMSResourceId.Text.Split('/').Last();

                        CredentialsEntryV3 entry = new CredentialsEntryV3(
                                                        new MediaService(null, form.textBoxAMSResourceId.Text, accountnamecc),
                                                        addaccount1.GetEnvironment(),
                                                        true,
                                                        form.radioButtonAADServicePrincipal.Checked,
                                                        form.textBoxAADtenantId.Text,
                                                        true
                                                        );

                        CredentialList.MediaServicesAccounts.Add(entry);
                        AddItemToListviewAccounts(entry);

                        SaveCredentialsToSettings();
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private void textBoxDescription_TextChanged(object sender, EventArgs e)
        {
            CredentialList.MediaServicesAccounts[listViewAccounts.SelectedIndices[0]].Description = textBoxDescription.Text;
            SaveCredentialsToSettings();
        }

        private void linkLabelAMSOfflineDoc_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Application.StartupPath, @"HelpFiles\", @"AMSv3doc.pdf"),
                UseShellExecute = true
            };
            p.Start();
        }


        private void ScaleListViewColumns(ListView listview)
        {
            listview.Columns[0].Width = listview.Width - 4 - SystemInformation.VerticalScrollBarWidth;
        }


        private void AmsLogin_DpiChangedAfterParent(object sender, EventArgs e)
        {
            ScaleListViewColumns(listViewAccounts);
        }

        private void AmsLogin_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            // DpiUtils.UpdatedSizeFontAfterDPIChange(labelenteramsacct, e);
        }
    }
}