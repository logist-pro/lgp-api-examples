using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace lgp_api_csharp
{
    [TestClass]
    public class ApiExample
    {
        // ������� ��������� �������
        private const string API_BASE_HOST = "https://...";
        private const string API_BASE_URL = API_BASE_HOST + "/api/v1/";
        // API ���� ������� �������� ��������
        private const string API_KEY = "...";
        // �����/������ ������������ �������� � ������������� ������� �������
        private const string LOGIN = "...";
        private const string PASSWORD = "...";


        [TestMethod]
        public async Task RunExample()
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                // �������� API ����� � ��������� HTTP ��������
                client.Headers.Add("X-ApiKey", API_KEY);


                // -----
                // 1. ������������ ������� (������ �� ������� �����������, ������ ���� API � ���������� �������)
                // -----
                var url = $"{API_BASE_URL}test/ping";
                Trace.WriteLine($"1. �������� ������� � API: {url}");
                // GET ������ �� ������
                // (������ ������������� ���������� ����������, ���� ������ ������ HttpCode �������� �� 200 Ok)
                await client.DownloadStringTaskAsync(url);
                Trace.WriteLine("   >> ���� ������");


                // -----
                // 2. ����������� � ������� �������� �������������
                // -----
                url = $"{API_BASE_URL}account/login";
                Trace.WriteLine($"2. �����������: {url}");
                // �������� ���������� � URL
                url += $"?login={HttpUtility.UrlEncode(LOGIN)}&password={HttpUtility.UrlEncode(PASSWORD)}";
                // POST ������ � ������ �����
                await client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, "");
                Trace.WriteLine("   >> ������ �������");
                // ����� Set-Cookie ��������� ������
                var cookie = client.ResponseHeaders?.Get("Set-Cookie");
                Assert.IsNotNull(cookie, "   >> [Error] cookie �� �����������");
                // �������� ������ ������������ ���� (.AspNet.ApplicationCookie)
                // ����������� � ��������� ���� ����������� HTTP ��������
                client.Headers.Add("Cookie", cookie.Split(';')[0]);


                // -----
                // 3. ����� ��������� ������ �������, ���������� ���������� �������� ��������� ������������ ����������
                //    (� ���������, ��������������� �������� ��������� � �������������� �� ����� ����)
                //    ���� ��� ��������� ����������, �� ����� ��������� ���������� ����������� � �������
                // -----
                url = $"{API_BASE_URL}tender/create";
                Trace.WriteLine($"3. ��������� ������������ ��� �������� �������: {url}");
                // ��������� ��������� �� ��������� ������ ����������
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                var json = await client.DownloadStringTaskAsync(url);
                Assert.IsNotNull(json, "   >> [Error] �� ������� ����������� �������� �����������");
                // ������ JSON ������ �������
                var dicts = JsonConvert.DeserializeObject<dynamic>(json);
                // ��������� ��������������� �� ������ ��������� ������������
                var corporateId = dicts.Corporates[0].Id;
                var contactId = dicts.Corporates[0].ContactPersons[0].Id;


                // -----
                // 4. �������� �������
                // -----
                Trace.WriteLine($"4. �������� �������: {url}");
                // ����� �������� ������
                var now = DateTime.Now;
                // ���� ���������� ������ (������� ���� +7 ����, 08:00)
                var start = now.Date.AddDays(7).AddHours(8);
                // ������ ��� �������� � ������� � ������� JSON
                var tender = new
                {
                    // ����� �������� ������ (����� ���� ����� ��������� ����)
                    OrderDate = now.ToString("O"),
                    // ���� ���������� ������
                    StartDate = start.ToString("O"),
                    Customer = new
                    {
                        CompanyId = corporateId,
                        ContactId = contactId
                    },
                    Cargo = "������ ����",
                    CargoWeight = 10,
                    CargoVolume = 10,
                    CargoDangerClass = 0,
                    // ������� ���� (�� ������� ���������� �������� ��������)
                    PackageType = "Joint",
                    RoutePoints = new[] {
                        new {
                            // ����� ��������
                            Type = "Loading",
                            Address = "������, ������� �������",
                            // ����� �������� = ���� ���������� ������
                            ArrivalTime = start.ToString("O"),
                            // ����� ������� +1 ���
                            LeaveTime = start.AddHours(1).ToString("O"),
                        },
                        new {
                            // ����� ��������
                            Type = "Unloading",
                            Address = "�����-���������, ��������� �������",
                            // ���� �� �������� ���������� �� ����� ��������, �� ������� ���������� �������������� ��������� ������� � ���������� ����������
                            // "Distance": 750,
                            ArrivalTime = start.AddHours(10).ToString("O"),
                            LeaveTime = start.AddHours(11).ToString("O"),
                        }
                    },
                    Tender = new
                    {
                        // ������ ������ ����� �� �������� ������
                        StartDate = now.ToString("O"),
                        // ����� ������ ����������� ������� �� 30 ����� �� ���������� ������
                        EndDate = start.AddHours(-1).ToString("O"),
                        InitCost = 50000,
                        // ����� � �������������� �������� ������������ ����
                        MinStepReq = "Auto",
                        VatReqs = "None",
                    }
                };
                // ��������� ��������� �� ������ ������������� ���� �������
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                var result = await client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, JsonConvert.SerializeObject(tender));
                Assert.IsNotNull(result, "   >> [Error] �� ������� ������������� ������ �������");
                // ��������� �������������� �������������� �������
                var tenderId = result.Trim('"');
                Trace.WriteLine($"   >> ������ ������ Id={tenderId}");


                // -----
                // 5. ��� �������� ������� ������ �������, ����������� ������ ������ �� ��������������
                // -----
                url = $"{API_BASE_URL}tender/{tenderId}";
                Trace.WriteLine($"5. �������� ������� �������: {url}");
                // ��������� ��������� �� ��������� ������ ����������
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                json = await client.DownloadStringTaskAsync(url);
                Assert.IsNotNull(json, "   >> [Error] �� �������� ��������� ���������� ������ �������");
                var details = JsonConvert.DeserializeObject<dynamic>(json);
                Trace.WriteLine($"   >> �����: {details.Number}");
                Trace.WriteLine($"   >> ������: {details.StatusTitle} ({details.Status})");
                Trace.WriteLine($"   >> ����� ���������� ��������� �������: {details.ActualDate} ({details.ActualDateTitle})");
                Trace.WriteLine($"   >> ����� ��������: {details.RouteLenght}");
                Trace.WriteLine($"   >> ���������� �����������: {details.ProposalsCount}");
                if (details.BestProposal != null)
                    Trace.WriteLine("   >> ������ �����������: {details.BestProposal.Bet}");
            }

        }
    }
}
