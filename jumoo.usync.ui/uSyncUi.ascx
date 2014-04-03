<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="uSyncUi.ascx.cs" Inherits="jumoo.usync.ui.uSyncUi" %>
<style>
    .setting-checkbox input {
        float: left;
        margin-left: 20px;
        margin-right: 20px;
    }
</style>
<script type="text/javascript">
    $(document.forms[0]).submit(function () {
        document.getElementById("usyncprogress").innerHTML
            = "Doing stuff... <small>can take a little while</small>";
        document.getElementById("usyncupdated").style.visibility = "hidden";
    });
</script>
<div id="usyncDashboard">
    <div class="propertypane">
        <div class="propertyItem">
            <div class="span12">
            <p>
                Out the box usync does everything automagically, if you want to
                have more control of what usync does, you can use this dashboard
            </p>
            <h3>uSync Settings:</h3>
            <p>untick all of these and usync will stop writing things to disk
                unless you tell it to. (the default is read and write on saves)
            </p>
            <ul class="unstyled">
                <li><asp:CheckBox ID="chkRead" runat="server" Text="Read on Startup" CssClass="setting-checkbox"/></li>
                <li><asp:CheckBox ID="chkWrite" runat="server" Text="Write on startup" CssClass="setting-checkbox" /></li>            
                <li><asp:CheckBox ID="chkAttatch" runat="server" Text="Write on Saves" CssClass="setting-checkbox" /></li>
                <li><asp:CheckBox ID="chkWatch" runat="server" Text="Watch the usync folder for changes and import them immediately" CssClass="setting-checkbox" /></li>
            </ul>
            <asp:Button ID="btnSave" runat="server" CssClass="btn btn-default" OnClick="btnSave_Click" Text="Save Settings" />
           </div>
           <div class="row">
            <div class="span6">
                <h3>uSync Import</h3>
                <p>
                    Run the full usync import on what ever is in the usync folder
                </p>
                <asp:Button ID="btnImport" runat="server" Text="Import" class="btn btn-info btn-lg" OnClick="btnImport_Click"/>
            </div>
            <div class="span6">
                <h3>uSync Export</h3>
                <p>
                    Write everything in this site into the usync folder, this will overwrite anything already there.
                </p>
                <asp:Button ID="btnExport" runat="server" Text="Export" class="btn btn-warning btn-lg" OnClick="btnExport_Click"/>
            </div>
           </div>
            <div class="row">
            <h3 id="usyncprogress"></h3>
            <asp:Panel ID="pnUpdate" runat="server" Visible="false">
            <div class="span12" id="usyncupdated">
                <h3>Done Stuff:</h3>
                <p class="alert alert-info">
                     <asp:Label ID="lbStatus" runat="server" Text=""></asp:Label>
                </p>
            </div>
            </asp:Panel>
            </div>
        </div>
    </div>

</div>

