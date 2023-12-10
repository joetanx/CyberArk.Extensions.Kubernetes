using System.Collections.Generic;
using CyberArk.Extensions.Plugins.Models;
using CyberArk.Extensions.Utilties.Logger;
using CyberArk.Extensions.Utilties.Reader;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using CyberArk.Extensions.Infra.Common;
using CyberArk.Extensions.Utilties;

// Change the Template name space
namespace CyberArk.Extensions.KubernetesServiceAccount
{
    public class Change : BaseAction
    {
        #region constructor
        /// <summary>
        /// Logon Ctor. Do not change anything unless you would like to initialize local class members
        /// The Ctor passes the logger module and the plug-in account's parameters to base.
        /// Do not change Ctor's definition not create another.
        /// <param name="accountList"></param>
        /// <param name="logger"></param>
        public Change(List<IAccount> accountList, ILogger logger)
            : base(accountList, logger)
        {
        }
        #endregion

        #region Setter
        /// <summary>
        /// Defines the Action name that the class is implementing - Change
        /// </summary>
        override public CPMAction ActionName
        {
            get { return CPMAction.changepass; }
        }
        #endregion

        /// <summary>
        /// Plug-in Starting point function.
        /// </summary>
        /// <param name="platformOutput"></param>

        static HttpClient ClientWithKubeToken(string targetAddr, string kubeToken)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => { return true; },
                // SslProtocols = SslProtocols.Tls13,
            };
            HttpClient client = new HttpClient(handler)
            {
                BaseAddress = new Uri(targetAddr),
            };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + kubeToken);
            return client;
        }
        static async Task<string> HttpPost(HttpClient client, string requestUri, string postBody)
        {
            StringContent postBodyJson = new StringContent(postBody, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(requestUri, postBodyJson);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            return responseBody;
        }

        override public int run(ref PlatformOutput platformOutput)
        {
            Logger.MethodStart();

            #region Init

            int RC = 9999;

            #endregion 

            try
            {

                #region Fetch Account Properties (FileCategories)

                // Example: Fetch mandatory parameter - Username.
                // A mandatory parameter is a parameter that must be defined in the account.
                // TargetAccount.AccountProp is a dictionary that provides access to all the file categories of the target account.
                // An exception will be thrown if the parameter does not exist in the account.
                string targetAddr = ParametersAPI.GetMandatoryParameter("address", TargetAccount.AccountProp);
                string targetUser = ParametersAPI.GetMandatoryParameter("username", TargetAccount.AccountProp);
                string targetNamespace = ParametersAPI.GetMandatoryParameter("scope", TargetAccount.AccountProp);
                string targetTokenDurationDay = ParametersAPI.GetMandatoryParameter("duration", TargetAccount.AccountProp);
                int targetTokenDurationSeconds = Convert.ToInt32(targetTokenDurationDay) * 24 * 3600;
                string kubeVersion = ParametersAPI.GetMandatoryParameter("keyid", TargetAccount.AccountProp);

                // Note: To fetch Logon, Reconcile, Master or Usage account properties,
                // replace the TargetAccount object with the relevant account's object.

                #endregion

                #region Fetch Account's Passwords

                // Example : Fetch the target account's password.
                string kubeToken = TargetAccount.CurrentPassword.convertSecureStringToString();

                #endregion

                #region Logic
                /////////////// Put your code here ////////////////////////////

                HttpClient client = ClientWithKubeToken(targetAddr, kubeToken);

                string postBodyCreateToken = "{\"kind\":\"TokenRequest\",\"apiVersion\":\"authentication.k8s.io/v1\",\"metadata\":{\"creationTimestamp\":null},\"spec\":{\"audiences\":null,\"expirationSeconds\":" + targetTokenDurationSeconds.ToString() + ",\"boundObjectRef\":null},\"status\":{\"token\":\"\",\"expirationTimestamp\":null}}";
                string requestUriCreateToken = "api/v1/namespaces/" + targetNamespace + "/serviceaccounts/" + targetUser + "/token";
                string responseCreateToken = HttpPost(client, requestUriCreateToken, postBodyCreateToken).GetAwaiter().GetResult();
                JObject responseCreateTokenJson = JObject.Parse(responseCreateToken);
                string keyInStringFormat = responseCreateTokenJson["status"]["token"].ToString();
                CASOSEncryptor encryptor = new CASOSEncryptor();
                List<string> encryptedDataList = new List<string>
                {
                    encryptor.Encrypt(kubeVersion),
                    encryptor.Encrypt(keyInStringFormat)
                };
                CPMEncryptedFileWriter cpmEncryptedFileWriter = new CPMEncryptedFileWriter();
                cpmEncryptedFileWriter.Write(encryptedDataList);

                RC = 0;
                client.Dispose();

                /////////////// Put your code here ////////////////////////////
                #endregion Logic

            }
            catch (Exception ex)
            {
                #region ErrorHandling
                switch (ex.Message)
                {
                    case string s when ex.Message.Contains("401 (Unauthorized)"):
                        RC = 8401;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid service account token";
                        break;
                    case string s when ex.Message.Contains("404 (Not Found)"):
                        RC = 8404;
                        platformOutput.Message = ex.Message + " Possible cause: Incorrect service account name or namespace";
                        break;
                    case string s when ex.Message.Contains("403 (Forbidden)"):
                        RC = 8403;
                        platformOutput.Message = ex.Message + " Possible cause: Insufficient permissions or missing authentication";
                        break;
                    case string s when ex.Message.Contains("422 (Unprocessable Entity)"):
                        RC = 8422;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid duration value";
                        break;
                    case string s when ex.Message.Contains("An error occurred while sending the request"):
                        RC = 8110;
                        platformOutput.Message = ex.Message + " Possible cause: Cluster connection error: unreachable cluster or SSL/TLS handshake failure";
                        break;
                    case string s when ex.Message.Contains("Value was either too large or too small for an Int32"):
                        RC = 8120;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid duration value";
                        break;
                    default:
                        RC = 8800;
                        platformOutput.Message = ex.Message + ". Unforseen error";
                        break;
                }
                #endregion ErrorHandling
            }
            finally
            {
                Logger.MethodEnd();
            }

            // Important:
            // 1.RC must be set to 0 in case of success, or 8000-9000 in case of an error.
            // 2.In case of an error, platformOutput.Message must be set with an informative error message, as it will be displayed to end user in PVWA.
            //   In case of success (RC = 0), platformOutput.Message can be left empty as it will be ignored.
            return RC;

        }

    }
}
