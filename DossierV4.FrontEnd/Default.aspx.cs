using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.IO;
using System.Threading;
using System.Web;

using DossierV4.Application;
using DossierV4.Tools.OpenId;

using DotNetOpenAuth.OpenId.Extensions.AttributeExchange;
using DotNetOpenAuth.OpenId.RelyingParty;
using NLog;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;


namespace DossierV4.FrontEnd
{
    public partial class Default : System.Web.UI.Page
    {
        private const string CallAddress = "/application/v3/tokeninfo";
        private const string CookieName = "dossier_token";
        private const string ModuleParamName = "module";
        private const string AppParamName = "application";
        private const string SsoAllowedHostsKey = "SsoAllowedHosts";

        private string _module;
        private readonly PowerSchoolProvider _provider;

        private static Logger Logger = LogManager.GetCurrentClassLogger();

        public string Domain = "";
        public string Token = "";

        public Default()
        {
            try
            {
                // Get allowed hosts from web.config file
                var appSet = ConfigurationManager.AppSettings[SsoAllowedHostsKey];
                _provider = appSet != null ? new PowerSchoolProvider(appSet.Split(',')) : new PowerSchoolProvider();
                // Set event handlers
                _provider.BeginRequest += provider_BeginRequest;
                _provider.EndRequest += provider_EndRequest;
                // Request info from PowerSchool OpenId provider
                _provider.FetchRequest.Attributes.AddRequired(PowerSchoolAttributes.Dcid);
                _provider.FetchRequest.Attributes.AddRequired(PowerSchoolAttributes.Reference);
                _provider.FetchRequest.Attributes.AddRequired(PowerSchoolAttributes.StudentIds);
                _provider.FetchRequest.Attributes.AddRequired(PowerSchoolAttributes.UserType);
            }
            catch (ThreadAbortException)
            {
                // Do nothing.  
                // No need to log exception when we expect ThreadAbortException
            }
            catch (Exception ex)
            {
                Logger.Trace("Exception in the Default.aspx constructor", ex);
                throw;
            }
            // Skip sertificate validation. !ONLY FOR DEBUG MODE. DON'T USE IN PRODUCTION!
#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
#endif
        }

        void provider_BeginRequest(object sender, PowerSchoolEventArgs e)
        {
            // Remember module name for redirecting
            var cookie = new HttpCookie(ModuleParamName, _module) { Expires = DateTime.Now.AddMinutes(2) };
            Response.SetCookie(cookie);
        }

        void provider_EndRequest(object sender, PowerSchoolEventArgs e)
        {
            _module = Request.Cookies[ModuleParamName] == null ? "" : Request.Cookies[ModuleParamName].Value;
            if (e.Status == AuthenticationStatus.Authenticated)
            {
                if (e.Data != null)
                {
                    var data = (FetchResponse)e.Data;
                    if (data.Attributes.Contains(PowerSchoolAttributes.UserType) &&
                        data.Attributes.Contains(PowerSchoolAttributes.Dcid))
                    {
                        var userType = data.Attributes[PowerSchoolAttributes.UserType].Values.FirstOrDefault();
                        var staffId = data.Attributes[PowerSchoolAttributes.Dcid].Values.FirstOrDefault();
                        int i;
                        Logger.Trace("SSO: Usertype {0}; staffId {1}", userType, staffId);
                        if (userType == "staff" || int.TryParse(staffId, out i))
                        {
                            var token = Authentication.GetSso(staffId);
                            if (token != null)
                            {
                                if (Request.QueryString[AppParamName] == null)
                                {
                                    Response.Redirect(string.IsNullOrEmpty(_module)
                                        ? AppRelativeVirtualPath
                                        : string.Format("{0}?module={1}", AppRelativeVirtualPath, _module), false);
                                }
                                else
                                {
                                    this.Token = token;
                                    this.Domain = e.Provider.Scheme + "://" + e.Provider.Host;
                                }
                            }

                            if(token == null && Request.QueryString[AppParamName] == null)
                            {
                                this.Token = "ERROR";
                                this.Domain = e.Provider.Scheme + "://" + e.Provider.Host;
                            }
                        }
                    }
                }
            }
            _module = "login";
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Logger.Trace("Page_Load Begin");
            try
            {
                _module = Request.QueryString[ModuleParamName] != null ? Request.QueryString[ModuleParamName].ToString() : "";
                Logger.Trace("Pagel_load SSO Process");
                if (!_provider.Process())
                {
                    Logger.Trace("Page_Load IsLogged check");
                    if (!IsLogged())
                    {
                        Logger.Trace("Page_Load RedirectToLogin");
                        RedirectToLogin();
                        Logger.Trace("Page_Load Return after redirect to login");
                        return;
                    }
                }
                Logger.Trace("Page)_load LoadHtml");
                if (Request.QueryString[AppParamName] == null)
                {
                    Response.Write(LoadHtml(_module));
                    Response.End();
                }
            }
            catch (ThreadAbortException)
            {
                // Do nothing.  
                // No need to log exception when we expect ThreadAbortException
            }
            catch (Exception ex)
            {
                Logger.Trace("Exception in the Default.aspx Load", ex);
                throw;
            }
        }

        private bool IsLogged()
        {
            HttpCookie cookie = Request.Cookies[CookieName];
            if (cookie == null)
            {
                Logger.Trace("Cookie is nulL");
                return false;
            }
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            HttpWebResponse response = null;
            string uri = ConfigurationManager.AppSettings["ServiceAddress"];
            try
            {
                Logger.Trace("IsLogged address: {0}{1}", uri, CallAddress);
                var request = (HttpWebRequest)WebRequest.Create(uri + CallAddress);
                request.KeepAlive = false;
                request.Timeout = 10 * 1000;
                request.Headers["Authorization"] = "Bearer " + cookie.Value;
                response = (HttpWebResponse)request.GetResponse();
                if (request.HaveResponse == false)
                {
                    return false;
                }
            }
            catch (WebException ex)
            {
                Logger.DebugException("IsLogged Failed", ex);
                if (ex.InnerException != null)
                {
                    Logger.DebugException("IsLogged Failed. Inner exception:", ex.InnerException);
                }
                return false;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
            return true;
        }

        private string LoadHtml(string module)
        {
            string fileName = "";
            switch (module)
            {
                case "":
                    fileName = "app.html"; break;
                case "login":
                    fileName = "login.html"; break;
                case "ipp":
                    fileName = "ipp.html"; break;
                case "pat":
                    fileName = "pat.html"; break;
                case "pat-summary":
                    fileName = "pat-summary.html"; break;
                case "slp":
                    fileName = "slp.html"; break;
                case "patsum":
                    fileName = "patsum.html"; break;
            }
            if (!string.IsNullOrEmpty(fileName) && File.Exists(MapPath(fileName)))
            {
                return File.ReadAllText(MapPath(fileName));
            }
            return "<h1>Error</h1>";
        }

        private void RedirectToLogin()
        {
            Response.Write(LoadHtml("login"));
            Response.End();
        }

#if DEBUG
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
#endif
    }
}