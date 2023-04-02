using DescriptiveDataUploadApp;
using Microsoft.Identity.Client;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using XAct;

namespace HttpClientCallerApp
{
    public class Program
    {
        private HttpResponseMessage message = null;
        private string pathToZippedFile = string.Empty;
        private string appId = string.Empty;
        private string tenantId = string.Empty;
        private string certName = string.Empty;
        private string connectorId = string.Empty;
        private string ingestionId = string.Empty;

        static void Main()
        {
            var program = new Program();
            program.RunAsync(program).GetAwaiter().GetResult();
        }

        public async Task RunAsync(Program prog, bool firstRun = true)
        {
            if (firstRun)
            {
                Console.WriteLine("Please enter the following values. \nNote: no quotation marks required around the responses.\n\n");
                Console.WriteLine("AppId/Client ID:");
                appId = Console.ReadLine() ?? appId;

                Console.WriteLine("\nAzure Active Directory (AAD) Tenant ID:");
                tenantId = Console.ReadLine() ?? tenantId;

                Console.WriteLine("\nCertificate name for your registered application:");
                certName = Console.ReadLine() ?? certName;

                if (appId == string.Empty || tenantId == string.Empty || certName == string.Empty)
                {
                    Console.WriteLine("\nNone of the inputs can be empty strings or nulls. \nPlease go through the process again to upload your file.\n");
                }
                else if (!IsGuid(appId) || !IsGuid(tenantId))
                {
                    Console.WriteLine("\nThe appId and/or the tenantId is not a valid Guid.\nPlease go through the process again to upload your file.\n");
                }
            }

            Console.WriteLine("\nEnter 1 if you would like to upload data.\nEnter 2 if you would like to check the status of an existing ingestion.\n");
            var inputChoice = Console.ReadLine().ToInt16();
            InputState uploadZipEnterDetails = inputChoice == 1 ? InputState.Chosen : InputState.NotChosen;
            InputState getStatusEnterDetails = inputChoice == 2 ? InputState.Chosen : InputState.NotChosen;

            if (uploadZipEnterDetails == InputState.Chosen)
            {
                UploadZipDetailsInput();
                uploadZipEnterDetails = InputState.EnteredValues;
            }

            if (getStatusEnterDetails == InputState.Chosen)
            {
                GetStatusDetailsInput();
                getStatusEnterDetails = InputState.EnteredValues;
            }

            var appToken = await prog.GetAppToken(tenantId, appId, certName);
            var bearerToken = string.Format("Bearer {0}", appToken);
            var apiToAccess = "";

            if (uploadZipEnterDetails == InputState.EnteredValues && getStatusEnterDetails == InputState.NotChosen) 
            {
                await PostUploadFile(prog, bearerToken, pathToZippedFile, tenantId);
            }
            else if (uploadZipEnterDetails == InputState.NotChosen && getStatusEnterDetails == InputState.EnteredValues)
            {
                await GetStatusOfUpload(prog, bearerToken, tenantId, connectorId, ingestionId);
            }
        }

        private void UploadZipDetailsInput()
        {
            Console.WriteLine("\nPlease enter the absolute path to the zipped file you wish to upload.\nFor example: C:\\\\Users\\\\JaneDoe\\\\OneDrive - Microsoft\\\\Desktop\\\\info.zip");
            pathToZippedFile = Console.ReadLine() ?? pathToZippedFile;

            if (pathToZippedFile == string.Empty)
            {
                Console.WriteLine("\nNone of the inputs can be empty strings or nulls. \nPlease go through the process again to upload your file.\n");
                return;
            }
        }

        private async Task GetStatusOfUpload(Program prog, string bearerToken, string tenantId, string connectorId, string ingestionId)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", bearerToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            var apiToAccess = string.Format(
                "{0}/{1}/ingress/connectors/{2}/ingestions/fileIngestion/{3}",
                Constants.NovaPrdApi,
                tenantId,
                connectorId,
                ingestionId);
            var message = await client.GetAsync(apiToAccess);
            await PrintOutput(prog, message, false, true);
        }

        private async Task PostUploadFile(Program prog, string bearerToken, string pathToZippedFile, string tenantId)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", bearerToken);
            var form = new MultipartFormDataContent();
            var byteArray = File.ReadAllBytes(pathToZippedFile);
            form.Add(new ByteArrayContent(byteArray, 0, byteArray.Length), "info", pathToZippedFile);
            var apiToAccess = string.Format(
                "{0}/{1}/ingress/connectors/HR/ingestions/fileIngestion",
                Constants.NovaPrdApi,
                tenantId);
            var message = await client.PostAsync(apiToAccess, form);
            await PrintOutput(prog, message, true, false);
        }

        private void GetStatusDetailsInput()
        {
            Console.WriteLine("Please enter the following values. \nNote: no quotation marks required around the responses.");
            Console.WriteLine("\nConnector ID:");
            connectorId = Console.ReadLine() ?? connectorId;

            Console.WriteLine("\nIngestion ID:");
            ingestionId = Console.ReadLine() ?? ingestionId;

            if (connectorId == string.Empty || ingestionId == string.Empty)
            {
                Console.WriteLine("\nNone of the inputs can be empty strings or nulls. \nPlease go through the process again.\n");
                return;
            }
        }

        private enum InputState
        {
            NotChosen,
            EnteredValues,
            Chosen
        }

        private async Task PrintOutput(Program prog, HttpResponseMessage message, bool showMainMenu, bool sourceIsGetStatus)
        {
            try
            {
                if (message.StatusCode == HttpStatusCode.OK)
                {
                    string responseBody = await message.Content.ReadAsStringAsync();
                    var contentForUpload = $"\nRequest Status was success.\nIngestion is in progress.\n\nHere is the returned content:\n {responseBody}.\n\nNote \"ConnectorId\" and \"Id(IngestionId)\" to ping for status.\n";
                    var contentForStatus = $"\nRequest Status was success.\nIngestion Status: {responseBody}.\n";
                    var contentToPrint = sourceIsGetStatus ? contentForStatus : contentForUpload;
                    Console.WriteLine(contentToPrint);
                    if (showMainMenu)
                    {
                        prog.RunAsync(prog, false).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    Console.WriteLine($"\nRequest Status was not successful:\n {message.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            Console.ReadLine();
        }

        private async Task<string> GetAppToken(string tenantId, string appId, string certName)
        {
            var authority = string.Format(
                "{0}/{1}",
                Constants.LoginBaseUrl,
                tenantId);

            var cert = FindCertificate(certName);
            var app = ConfidentialClientApplicationBuilder.Create(appId)
                         .WithCertificate(cert)
                         .WithAuthority(new Uri(authority))
                         .Build();

            string appToken = string.Empty;
            try
            {
                var authResult = await app.AcquireTokenForClient(
                    new[] { $"{Constants.NovaPrdUri}/.default" })
                    .WithSendX5C(true)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                appToken = authResult.AccessToken;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return appToken;
        }

        private static X509Certificate2 FindCertificate(string certificateName)
        {
            using var localStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            using var currentStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);

            try
            {
                localStore.Open(OpenFlags.ReadOnly);
                var localCert = localStore.Certificates
                    .FirstOrDefault(c => c.SubjectName.Name == certificateName);

                currentStore.Open(OpenFlags.ReadOnly);
                var currentCert = currentStore.Certificates
                    .FirstOrDefault(c => c.SubjectName.Name == certificateName);

                if (localCert == null && currentCert == null)
                {
                    throw new InvalidOperationException($"\nFailed to load the certificate with find name {certificateName}");
                }

                return localCert != null ? localCert : currentCert;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"\nFailed to load the certificate with find name {certificateName}", ex);
            }
            finally
            {
                localStore.Close();
                currentStore.Close();
            }
        }

        private static bool IsGuid(string value)
        {
            Guid x;
            return Guid.TryParse(value, out x);
        }
    }
}
