<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="uSyncDashboard.ascx.cs" Inherits="jumoo.usync.ui.uSyncDashboard" %>
<%@ Register Namespace="ClientDependency.Core.Controls" Assembly="ClientDependency.Core" TagPrefix="cdf" %>

<cdf:CssInclude ID="CssInclude2" runat="server" FilePath="plugins/usync/usyncdash.css" PathNameAlias="UmbracoRoot"  />

<script>
    $(document.forms[0]).submit(function () {
        document.getElementById("usyncProgress").style.display = "block";
        var rw = document.getElementById("resultsWrapper");
        if (rw) {
            rw.style.display = "none";
        }
    });
</script>

<div class="propertypane">
    <div class="propertyitem">
        <div class="dashboardWrapper">
            <h2>uSync</h2>
            <img src="/umbraco/dashboard/images/zipfile.png" alt="usync" class="dashboardIcon" />
    
            <asp:UpdatePanel ID="UpdatePanel1" runat="server">
                <ContentTemplate>
            <div class="dashboardColWrapper">
                <div class="dashboardCols">
                    <div class="dashboardCol third">
                        <h3>uSync Settings</h3>
                        <div class="uSync-col">
                            <ul class="list-none">
                                <li><asp:CheckBox ID="chkRead" runat="server" Text="Import at startup" CssClass="settings-checkbox" /></li>
                                <li><asp:CheckBox ID="chkWrite" runat="server" Text="Export at startup" CssClass="settings-checkbox" /></li>
                                <li><asp:CheckBox ID="chkAttach" runat="server" Text="Export on Save" CssClass="settings-checkbox" /></li>
                                <li><asp:CheckBox ID="chkWatch" runat="server" Text="Import on file Change" CssClass="settings-checkbox" /></li>
                            </ul>
                        </div>
                        <asp:Button ID="btnSave" runat="server" CssClass="btn btn-default" Text="Save Settings" OnClick="btnSave_Click" />
                    </div>

                    <div class="dashboardCol third">
                        <h3>Run Import</h3>
                        <div class="uSync-col">
                            <p>Run a full usync import and bring in what ever is in the uSync folder</p>
                            <ul class="list-none">
                                <li><asp:CheckBox ID="chkImportReport" runat="server" Text="Report Only" /></li>
                                <li><asp:CheckBox ID="chkForceImport" runat="server" Text="Full Import" /></li>
                            </ul>
                        </div>
                        <asp:Button ID="btnImport" runat="server" Text="Import" CssClass="btn btn-info btn-large" OnClick="btnImport_Click" />
                    </div>
                    <div class="dashboardCol third">
                        <h3>Run Export</h3>
                        <div class="uSync-col">
                            <p>Create a new export:</p>
                            <p>A new export will delete the usync folder, and create everything anew</p>
                        </div>
                        <asp:Button ID="btnExport" runat="server" Text="Export" CssClass="btn btn-info btn-large" OnClick="btnExport_Click" />
                    </div>
                </div>
            </div>
            <div id="usyncProgress" style="display:none;" >
                Processing .... <img src="~/umbraco_client/images/progressBar.gif" runat="server" alt="loading..." />
            </div>

           <asp:Panel ID="resultsPanel" runat="server" Visible="false">
           <div class="dashboardColWrapper" id="resultsWrapper">
                <div class="dashboardCols">
                    <div class="dashboardCol">
                        <h3><asp:Label ID="status" runat="server"></asp:Label></strong></h3>

                        <asp:Repeater ID="resultsRpt" runat="server">
                            <HeaderTemplate>
                                <table class="sync-summary">
                                    <thead>
                                        <tr>
                                            <th>Name</th>
                                            <th>Type</th>
                                            <th>Change</th>
                                            <th>Message</th>
                                       </tr>
                                    </thead>
                            </HeaderTemplate>
                            <ItemTemplate>
                                <tr>
                                    <td><%# DataBinder.Eval(Container.DataItem, "name") %></td>
                                    <td><%# DataBinder.Eval(Container.DataItem, "itemType") %></td>
                                    <td><%# DataBinder.Eval(Container.DataItem, "changeType") %></td>
                                    <td><%# DataBinder.Eval(Container.DataItem, "message") %></td>
                                </tr>
                            </ItemTemplate>
                            <FooterTemplate>
                                </table>
                            </FooterTemplate>
                        </asp:Repeater>
                        </div>
                    </div>
               </div>
            </asp:Panel>

               <div class="dashboardColWrapper">
                    <div class="dashboardCols">
                        <div class="dashboardCol">
                            <h3>Recent changes</h3>
                            <asp:Repeater id="repRecent" runat="server">
                                <HeaderTemplate><ul></HeaderTemplate>
                                <ItemTemplate>
                                          <li><%# Container.DataItem.ToString() %></li>
                                </ItemTemplate>
                                <FooterTemplate></ul></FooterTemplate>
                            </asp:Repeater>
                        </div>
                    </div>
                </div>
            </ContentTemplate>
            </asp:UpdatePanel>

        </div>
    </div>
</div>
