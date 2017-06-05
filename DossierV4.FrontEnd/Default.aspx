<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="DossierV4.FrontEnd.Default" %>
<html>
<head>
    <script type="text/javascript">
        if ('<%= this.Token %>' !== '' && '<%= this.Domain %>' !== '') {
            parent.window.postMessage('token:' + '<%= this.Token %>', '<%= this.Domain %>');
        }
    </script>
</head>
<body>
</body>
</html>
