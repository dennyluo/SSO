using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using NLog;

namespace DossierV4.FrontEnd
{
    public class Global : System.Web.HttpApplication
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();
        protected void Application_Start(object sender, EventArgs e)
        {

        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            
        }
        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            Logger.FatalException("Application exception: " + ex.Message, null);
        }
        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

       

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {

        }
    }
}