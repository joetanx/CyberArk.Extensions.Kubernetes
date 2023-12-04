using System.Collections.Generic;
using CyberArk.Extensions.Plugins.Models;
using CyberArk.Extensions.Utilties.Logger;
using CyberArk.Extensions.Utilties.Reader;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using System.Threading.Tasks;
using CyberArk.Extensions.Infra.Common;
using CyberArk.Extensions.Utilties;

// Change the Template name space
namespace CyberArk.Extensions.KubernetesServiceAccount
{
    public class Reconcile : BaseAction
    {
        #region constructor
        /// <summary>
        /// Logon Ctor. Do not change anything unless you would like to initialize local class members
        /// The Ctor passes the logger module and the plug-in account's parameters to base.
        /// Do not change Ctor's definition not create another.
        /// <param name="accountList"></param>
        /// <param name="logger"></param>
        public Reconcile(List<IAccount> accountList, ILogger logger)
            : base(accountList, logger)
        {
        }
        #endregion

        #region Setter
        /// <summary>
        /// Defines the Action name that the class is implementing - Reconcile
        /// </summary>
        override public CPMAction ActionName
        {
            get { return CPMAction.reconcilepass; }
        }
        #endregion

        /// <summary>
        /// Plug-in Starting point function.
        /// </summary>
        /// <param name="platformOutput"></param>

        static HttpClient ClientWithCert(string targetAddr, X509Certificate2 clientP12)
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, certificate, chain, errors) => { return true; },
                // ClientCertificateOptions = ClientCertificateOption.Manual,
                // SslProtocols = SslProtocols.Tls13,
                ClientCertificates = { clientP12 }
            };
            HttpClient client = new HttpClient(handler)
            {
                BaseAddress = new Uri(targetAddr),
            };
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
        static Org.BouncyCastle.X509.X509Certificate ReadCertificate(string clientCert)
        { // Convert client certificate from a base64 encoded string to a BouncyCastle X509Certificate object
            byte[] clientCertBytes = Convert.FromBase64String(clientCert);
            X509CertificateParser certParser = new X509CertificateParser();
            return certParser.ReadCertificate(clientCertBytes);
        }
        static Org.BouncyCastle.Crypto.AsymmetricKeyParameter ReadPrvKey(string prvKey)
        { // Convert client key from a base64 encoded string to a BouncyCastle AsymmetricKeyParameter object
            byte[] prvKeyDecoded = Convert.FromBase64String(prvKey);
            MemoryStream stream = new MemoryStream(prvKeyDecoded);
            StreamReader reader = new StreamReader(stream);
            PemReader pemReader = new PemReader(reader);
            object pemReaderObject = pemReader.ReadObject();
            if (pemReaderObject is AsymmetricCipherKeyPair)
            {
                // If PRVKEY is in PKCS1 format (-----BEGIN RSA PRIVATE KEY----- ... -----END RSA PRIVATE KEY-----), the output of PemReader is AsymmetricCipherKeyPair
                // Need to get the .Private property to get AsymmetricKeyParameter
                AsymmetricCipherKeyPair keyPair = (AsymmetricCipherKeyPair)pemReaderObject;
                return keyPair.Private;
            }
            else if (pemReaderObject is AsymmetricKeyParameter)
            {
                // If PRVKEY is in PKCS8 format (-----BEGIN PRIVATE KEY----- ... -----END PRIVATE KEY-----), the output of PemReader is AsymmetricKeyParameter
                return (AsymmetricKeyParameter)pemReaderObject;
            }
            else
            {
                throw new Exception("Invalid certificate or key");
            }
        }
        static X509Certificate2 MakeCertificate(Org.BouncyCastle.X509.X509Certificate bcClientCert, AsymmetricKeyParameter keyParam)
        { // Combine BouncyCastle X509Certificate and AsymmetricKeyParameter into System.Security.Cryptography.X509Certificates.X509Certificate2 object to use with HttpClient
            Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
            builder.SetUseDerEncoding(true);
            Pkcs12Store store = builder.Build();
            X509CertificateEntry certEntry = new X509CertificateEntry(bcClientCert);
            store.SetCertificateEntry(string.Empty, certEntry);
            store.SetKeyEntry(string.Empty, new AsymmetricKeyEntry(keyParam), new X509CertificateEntry[] { certEntry });
            MemoryStream stream = new MemoryStream();
            store.Save(stream, Array.Empty<char>(), new SecureRandom());
            return new X509Certificate2(stream.GetBuffer(), string.Empty, X509KeyStorageFlags.DefaultKeySet);
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
                // ReconcileAccount.AccountProp is a dictionary that provides access to all the file categories of the reconcile account.
                // An exception will be thrown if the parameter does not exist in the account.
                string targetAddr = ParametersAPI.GetMandatoryParameter("address", TargetAccount.AccountProp);
                string targetUser = ParametersAPI.GetMandatoryParameter("username", TargetAccount.AccountProp);
                string targetNamespace = ParametersAPI.GetMandatoryParameter("keyid", TargetAccount.AccountProp);
                string targetTokenDurationDay = ParametersAPI.GetMandatoryParameter("duration", TargetAccount.AccountProp);
                int targetTokenDurationSeconds = Convert.ToInt32(targetTokenDurationDay) * 24 * 3600;

                // Note: To fetch Logon, Reconcile, Master or Usage account properties,
                // replace the TargetAccount object with the relevant account's object.

                #endregion

                #region Fetch Account's Passwords

                // Example : Fetch the reconcile account's password.
                string kcEncoded = ReconcileAccount.CurrentPassword.convertSecureStringToString();

                #endregion

                #region Logic
                /////////////// Put your code here ////////////////////////////

                // Deserialize kubeconfig
                string kcString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(kcEncoded));
                IDeserializer deserializer = new DeserializerBuilder().Build();
                dynamic kcDeconstructed = deserializer.Deserialize<dynamic>(kcString);

                // Prepare client certificate and key into P12 to be used with HttpClient
                // For some reason, reading certificate fails is it is the first certificate in the user store, "placeholder" serves as a "padding" so the actual "clientP12" will not be the first certificate in the user store
                Org.BouncyCastle.X509.X509Certificate bcClientCert = ReadCertificate(kcDeconstructed["users"][0]["user"]["client-certificate-data"]);
                AsymmetricKeyParameter keyParam = ReadPrvKey(kcDeconstructed["users"][0]["user"]["client-key-data"]);
                X509Certificate2 placeholder = MakeCertificate(bcClientCert, keyParam);
                X509Certificate2 clientP12 = MakeCertificate(bcClientCert, keyParam);

                HttpClient client = ClientWithCert(targetAddr, clientP12);

                string postBodyCreateToken = "{\"kind\":\"TokenRequest\",\"apiVersion\":\"authentication.k8s.io/v1\",\"metadata\":{\"creationTimestamp\":null},\"spec\":{\"audiences\":null,\"expirationSeconds\":" + targetTokenDurationSeconds.ToString() + ",\"boundObjectRef\":null},\"status\":{\"token\":\"\",\"expirationTimestamp\":null}}";
                string requestUriCreateToken = "api/v1/namespaces/" + targetNamespace + "/serviceaccounts/" + targetUser + "/token";
                string responseCreateToken = HttpPost(client, requestUriCreateToken, postBodyCreateToken).GetAwaiter().GetResult();
                JObject responseCreateTokenJson = JObject.Parse(responseCreateToken);
                string keyInStringFormat = responseCreateTokenJson["status"]["token"].ToString();
                CASOSEncryptor encryptor = new CASOSEncryptor();
                List<string> encryptedDataList = new List<string>
                {
                    encryptor.Encrypt(targetNamespace),
                    encryptor.Encrypt(keyInStringFormat)
                };
                CPMEncryptedFileWriter cpmEncryptedFileWriter = new CPMEncryptedFileWriter();
                cpmEncryptedFileWriter.Write(encryptedDataList);

                RC = 0;
                client.Dispose();
                placeholder.Dispose();
                clientP12.Dispose();

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
                    case string s when ex.Message.Contains("Base-64"):
                        RC = 8130;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid Base-64 encoding of certficate or key";
                        break;
                    case string s when ex.Message.Contains("Invalid certificate or key"):
                        RC = 8140;
                        platformOutput.Message = ex.Message;
                        break;
                    case string s when ex.Message.Contains("Failed to read certificate"):
                        RC = 8150;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid Base-64 encoding of certficate";
                        break;
                    case string s when ex.Message.Contains("KEY was not found"):
                        RC = 8160;
                        platformOutput.Message = ex.Message + " Possible cause: Invalid Base-64 encoding of key";
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
