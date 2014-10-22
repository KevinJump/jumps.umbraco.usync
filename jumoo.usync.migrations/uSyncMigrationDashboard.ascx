<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="uSyncMigrationDashboard.ascx.cs" Inherits="jumoo.usync.migrations.uSyncMigrationDashboard" %>
<%@ Register Namespace="ClientDependency.Core.Controls" Assembly="ClientDependency.Core" TagPrefix="cdf" %>

<div class="propertypane">
    <div class="propertyitem">
        <div class="dashboardWrapper">
            <h2>uSync.Migrations</h2>
            <img src="./dashboard/images/zipfile.png" alt="usync" class="dashboardIcon" />
            <p>
                uSync migrations is a diffrent way of using usync.
            </p>

            <div class="dashboardColWrapper">
                <div class="dashboardCols">
                    <div class="dashboardCol">
                        <h3>Manage Migrations</h3>
                        <p>
                            Here you can view your migrations...
                        </p>
                        <asp:Repeater ID="rptListMigrations" runat="server">
                            <HeaderTemplate>
                                <ul>
                            </HeaderTemplate>
                            <ItemTemplate>
                                
                            </ItemTemplate>
                            <FooterTemplate>
                                </ul>
                            </FooterTemplate>
                        </asp:Repeater>
                        <asp:Button ID="btnCreate" runat="server" Text="Create a migration" OnClick="btnCreate_Click" />
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>