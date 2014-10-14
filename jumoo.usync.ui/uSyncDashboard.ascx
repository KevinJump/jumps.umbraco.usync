<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="uSyncDashboard.ascx.cs" Inherits="jumoo.usync.ui.uSyncDashboard" %>
<%@ Register Namespace="ClientDependency.Core.Controls" Assembly="ClientDependency.Core" TagPrefix="cdf" %>

<cdf:CssInclude ID="CssInclude2" runat="server" FilePath="plugins/usync/usyncdash.css" PathNameAlias="UmbracoRoot"  />

<div class="propertypane">
    <div class="propertyitem">
        <div class="dashboardWrapper">
            <h2>uSync</h2>
            <img src="./dashboard/images/zipfile.png" alt="usync" class="dashboardIcon" />
            <h3>Welcome to the usync dashboard,</h3>
            <p>
                This dashboard lets you see the status of uSync and control some of the basic
                settings.
            </p>
            <h3>uSync Settings</h3>
            <div class="dashboardColWrapper">
                <div class="dashboardCols">
                    <div class="dashboardCol third">
                        <ul class="list-none">
                            <li><asp:CheckBox ID="chkRead" runat="server" Text="Import at startup" CssClass="settings-checkbox" /></li>
                            <li><asp:CheckBox ID="chkWrite" runat="server" Text="Export at startup" CssClass="settings-checkbox" /></li>
                            <li><asp:CheckBox ID="chkAttach" runat="server" Text="Export on Save" CssClass="settings-checkbox" /></li>
                            <li><asp:CheckBox ID="chkWatch" runat="server" Text="Import on file Change" CssClass="settings-checkbox" /></li>
                        </ul>

                        <asp:Button ID="btnSave" runat="server" CssClass="btn btn-default" Text="Save Settings" />
                    </div>

                    <div class="dashboardCol third">
                        <h3>Run Import</h3>
                        <p>Run a full usync import and bring in what ever is in the uSync folder</p>
                        <asp:Button ID="btnImport" runat="server" Text="Import" CssClass="btn btn-info btn-large" />
                    </div>
                    <div class="dashboardCol third">
                        <h3>Run Export</h3>
                        <p>Create a new export - this will overwrite whatever is on the disk</p>
                        <asp:Button ID="btnExport" runat="server" Text="Export" CssClass="btn btn-info btn-large" />
                    </div>
                </div>
            </div>

            <h3>Status</h3>
            <p>The status of the last few imports</p>

            <h3>Snapshots</h3>
            <p>
                With snapshots you can choose a small folder/or file to import
            </p>
        </div>
    </div>
</div>
