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
        // базовые настройки доступа
        private const string API_BASE_HOST = "https://...";
        private const string API_BASE_URL = API_BASE_HOST + "/api/v1/";
        // API ключ личного кабинета компании
        private const string API_KEY = "...";
        // логин/пароль технического аккаунта с максимальными правами доступа
        private const string LOGIN = "...";
        private const string PASSWORD = "...";


        [TestMethod]
        public async Task RunExample()
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                // передача API ключа в заголовке HTTP запросов
                client.Headers.Add("X-ApiKey", API_KEY);


                // -----
                // 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
                // -----
                var url = $"{API_BASE_URL}test/ping";
                Trace.WriteLine($"1. Проверка доступа к API: {url}");
                // GET запрос на сервер
                // (клиент автоматически сформирует исключение, если сервер вернет HttpCode отличный от 200 Ok)
                await client.DownloadStringTaskAsync(url);
                Trace.WriteLine("   >> есть доступ");


                // -----
                // 2. авторизация в системе заданным пользователем
                // -----
                url = $"{API_BASE_URL}account/login";
                Trace.WriteLine($"2. Авторизация: {url}");
                // передача параметров в URL
                url += $"?login={HttpUtility.UrlEncode(LOGIN)}&password={HttpUtility.UrlEncode(PASSWORD)}";
                // POST запрос с пустым телом
                await client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, "");
                Trace.WriteLine("   >> прошла успешно");
                // поиск Set-Cookie заголовка ответа
                var cookie = client.ResponseHeaders?.Get("Set-Cookie");
                Assert.IsNotNull(cookie, "   >> [Error] cookie не установлена");
                // значения первой возвращенной куки (.AspNet.ApplicationCookie)
                // добавляется в заголовки всех последующих HTTP запросов
                client.Headers.Add("Cookie", cookie.Split(';')[0]);


                // -----
                // 3. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
                //    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
                //    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера
                // -----
                url = $"{API_BASE_URL}tender/create";
                Trace.WriteLine($"3. Получение справочников для создания запроса: {url}");
                // заголовок указывает на ожидаемый формат результата
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                var json = await client.DownloadStringTaskAsync(url);
                Assert.IsNotNull(json, "   >> [Error] не найдены необходимые элементы справочника");
                // разбор JSON ответа сервера
                var dicts = JsonConvert.DeserializeObject<dynamic>(json);
                // получение идентификаторов из первых элементов справочников
                var corporateId = dicts.Corporates[0].Id;
                var contactId = dicts.Corporates[0].ContactPersons[0].Id;


                // -----
                // 4. Создание запроса
                // -----
                Trace.WriteLine($"4. Создание запроса: {url}");
                // время создания заказа
                var now = DateTime.Now;
                // дата исполнения заказа (текущая дата +7 дней, 08:00)
                var start = now.Date.AddDays(7).AddHours(8);
                // объект для передачи в запросе в формате JSON
                var tender = new
                {
                    // время создания заказа (может быть любая прошедшая дата)
                    OrderDate = now.ToString("O"),
                    // дата исполнения заказа
                    StartDate = start.ToString("O"),
                    Customer = new
                    {
                        CompanyId = corporateId,
                        ContactId = contactId
                    },
                    Cargo = "Важный груз",
                    CargoWeight = 10,
                    CargoVolume = 10,
                    CargoDangerClass = 0,
                    // сборный груз (не требует детального описания упаковки)
                    PackageType = "Joint",
                    RoutePoints = new[] {
                        new {
                            // точка погрузки
                            Type = "Loading",
                            Address = "Москва, Красная площадь",
                            // время прибытия = дата исполнения заказа
                            ArrivalTime = start.ToString("O"),
                            // время отбытия +1 час
                            LeaveTime = start.AddHours(1).ToString("O"),
                        },
                        new {
                            // точка выгрузки
                            Type = "Unloading",
                            Address = "Санкт-Петербург, Дворцовая площадь",
                            // если не указывть расстояние до точки маршрута, то система попытается самостоятельно построить маршрут и определить расстояние
                            // "Distance": 750,
                            ArrivalTime = start.AddHours(10).ToString("O"),
                            LeaveTime = start.AddHours(11).ToString("O"),
                        }
                    },
                    Tender = new
                    {
                        // запуск торгов сразу по созданию заказа
                        StartDate = now.ToString("O"),
                        // торги должны закончиться минимум за 30 минут до исполнения заказа
                        EndDate = start.AddHours(-1).ToString("O"),
                        InitCost = 50000,
                        // торги с автоматическим подбором минимального шага
                        MinStepReq = "Auto",
                        VatReqs = "None",
                    }
                };
                // заголовок указывает на формат передаваемого тела запроса
                client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                var result = await client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, JsonConvert.SerializeObject(tender));
                Assert.IsNotNull(result, "   >> [Error] не получен идентификатор нового запроса");
                // получение идентификатора новосозданного запроса
                var tenderId = result.Trim('"');
                Trace.WriteLine($"   >> Создан запрос Id={tenderId}");


                // -----
                // 5. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
                // -----
                url = $"{API_BASE_URL}tender/{tenderId}";
                Trace.WriteLine($"5. Проверка статуса запроса: {url}");
                // заголовок указывает на ожидаемый формат результата
                client.Headers.Add(HttpRequestHeader.Accept, "application/json");
                json = await client.DownloadStringTaskAsync(url);
                Assert.IsNotNull(json, "   >> [Error] не получена детальная информация нового запроса");
                var details = JsonConvert.DeserializeObject<dynamic>(json);
                Trace.WriteLine($"   >> Номер: {details.Number}");
                Trace.WriteLine($"   >> Статус: {details.StatusTitle} ({details.Status})");
                Trace.WriteLine($"   >> Время последнего изменения статуса: {details.ActualDate} ({details.ActualDateTitle})");
                Trace.WriteLine($"   >> Длина маршрута: {details.RouteLenght}");
                Trace.WriteLine($"   >> Количество предложений: {details.ProposalsCount}");
                if (details.BestProposal != null)
                    Trace.WriteLine($"   >> Лучшее предложение: {details.BestProposal.Bet}");
            }

        }
    }
}
