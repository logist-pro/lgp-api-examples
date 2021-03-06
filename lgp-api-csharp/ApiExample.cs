﻿using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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

        /// <summary>
        /// Глобальный объект клиента для web запросов с базовыми настройками
        /// </summary>
        static WebClient Client { get; } = new WebClient
        {
            Encoding = Encoding.UTF8,
            Headers = new WebHeaderCollection
            {
                // передача API ключа в заголовке HTTP запросов
                { "X-ApiKey", API_KEY }
            }
        };

        /// <summary>
        /// Метод инициализации класса (исполняется один раз перед всеми тестами).
        /// Осуществляет авторизацию в системе.
        /// </summary>
        [ClassInitialize]
        public static async Task Login(TestContext _)
        {
            // -----
            // 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
            // -----
            var url = $"{API_BASE_URL}test/ping";
            Trace.WriteLine($"1. Проверка доступа к API: {url}");
            // GET запрос на сервер
            // (клиент автоматически сформирует исключение, если сервер вернет HttpCode отличный от 200 Ok)
            await Client.DownloadStringTaskAsync(url);
            Trace.WriteLine("   >> есть доступ");

            // -----
            // 2. авторизация в системе заданным пользователем
            // -----
            url = $"{API_BASE_URL}account/login";
            Trace.WriteLine($"2. Авторизация: {url}");
            // передача параметров в теле запроса
            var login = new {
                Login = LOGIN,
                Password = PASSWORD
            };
            // заголовок указывает на формат передаваемого тела запроса
            Client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            // POST запрос с json телом
            await Client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, JsonConvert.SerializeObject(login));
            Trace.WriteLine("   >> прошла успешно");
            // поиск Set-Cookie заголовка ответа
            var cookie = Client.ResponseHeaders?.Get("Set-Cookie");
            Assert.IsNotNull(cookie, "   >> [Error] cookie не установлена");
            // значения первой возвращенной куки (.AspNet.ApplicationCookie)
            // добавляется в заголовки всех последующих HTTP запросов
            Client.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        /// <summary>
        /// Пример создания запроса на торги с проверкой результата
        /// </summary>
        [TestMethod]
        public async Task Test1_CreateTender()
        {
            // -----
            // 3. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
            //    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
            //    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера
            // -----
            var dictionaries = await GetTenderDictionaries();
            // получение идентификаторов из первых элементов справочников
            var corporateId = dictionaries.Corporates[0].Id;
            var contactId = dictionaries.Corporates[0].ContactPersons[0].Id;


            // -----
            // 4. Создание запроса
            // -----
            var url = $"{API_BASE_URL}tender/create";
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
                // детальное описание упаковки
                PackageDetails = new [] {
                    new {
                        Type = "Pallets",
                        Number = 12
                    }
                },
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
                },
                // требования к транспорту
                TransportRequirements = new
                {
                    TransportType = "Auto",
                    BodyType = "Tent"
                }
            };
            // заголовок указывает на формат передаваемого тела запроса
            Client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            var result = await Client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, JsonConvert.SerializeObject(tender));
            Assert.IsNotNull(result, "   >> [Error] не получен идентификатор нового запроса");
            // получение идентификатора новосозданного запроса
            var tenderId = Guid.Parse(result.Trim('"'));
            Trace.WriteLine($"   >> Создан запрос Id={tenderId}");


            // -----
            // 5. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
            // -----
            var details = await GetTenderDetails(tenderId);
            // статус у новосозданного запроса должен быть Разыгрывается (Awaiting)
            Assert.AreEqual(details.Status.ToString(), "Awaiting");
        }

        /// <summary>
        /// Пример прямого назначения перевозки на подрядчика
        /// </summary>
        [TestMethod]
        public async Task Test2_AssignTender()
        {
            // -----
            // 6. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
            //    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
            //    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера.
            //    Для назначения запроса, необходимо знать идентификатор подрядчика
            // -----
            var dictionaries = await GetTenderDictionaries();
            // получение идентификаторов из первых элементов справочников
            var corporateId = dictionaries.Corporates[0].Id;
            var contactId = dictionaries.Corporates[0].ContactPersons[0].Id;
            var contractorId = dictionaries.Contractors[0].Id;


            // -----
            // 7. Создание запроса с назначением
            // -----
            var url = $"{API_BASE_URL}tender/assign";
            Trace.WriteLine($"7. Создание запроса с назначением: {url}");
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
                // детальное описание упаковки
                PackageDetails = new [] {
                    new {
                        Type = "Pallets",
                        Number = 12
                    }
                },
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
                // требования к транспорту
                TransportRequirements = new
                {
                    TransportType = "Auto",
                    BodyType = "Tent"
                },
                // идентификатор подрядчика
                ContractorId = contractorId,
                // обязательное указание стоимости перевозки
                Cost = 30000,
            };
            // заголовок указывает на формат передаваемого тела запроса
            Client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            var result = await Client.UploadStringTaskAsync(url, WebRequestMethods.Http.Post, JsonConvert.SerializeObject(tender));
            Assert.IsNotNull(result, "   >> [Error] не получен идентификатор нового запроса");
            // получение идентификатора новосозданного запроса
            var tenderId = Guid.Parse(result.Trim('"'));
            Trace.WriteLine($"   >> Создан запрос Id={tenderId}");


            // -----
            // 8. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
            // -----
            var details = await GetTenderDetails(tenderId);
            // статус у назначенного запроса должен быть На согласовании (Approving)
            Assert.AreEqual(details.Status.ToString(), "Approving");
        }

        /// <summary>
        /// Метод получения справочников, необходимых для создания нового запроса
        /// </summary>
        /// <returns>Динамический объект с коллекциями справочников</returns>
        async Task<dynamic> GetTenderDictionaries()
        {
            var url = $"{API_BASE_URL}tender/create";
            Trace.WriteLine($"3. Получение справочников для создания запроса: {url}");
            // заголовок указывает на ожидаемый формат результата
            Client.Headers.Add(HttpRequestHeader.Accept, "application/json");
            var json = await Client.DownloadStringTaskAsync(url);
            Assert.IsNotNull(json, "   >> [Error] не найдены справочники");
            // разбор JSON ответа сервера
            var dictionaries = JsonConvert.DeserializeObject<dynamic>(json);

            return dictionaries;
        }
        /// <summary>
        /// Метод получения детальной информации по запросу
        /// </summary>
        /// <param name="tenderId">Идентификатор запроса</param>
        /// <returns>Динамический объект с детальной информацией</returns>
        async Task<dynamic> GetTenderDetails(Guid tenderId)
        {
            var url = $"{API_BASE_URL}tender/{tenderId}";
            Trace.WriteLine($"5. Проверка статуса запроса: {url}");
            // заголовок указывает на ожидаемый формат результата
            Client.Headers.Add(HttpRequestHeader.Accept, "application/json");
            var json = await Client.DownloadStringTaskAsync(url);
            Assert.IsNotNull(json, "   >> [Error] не получена детальная информация нового запроса");
            var details = JsonConvert.DeserializeObject<dynamic>(json);
            Trace.WriteLine($"   >> Номер: {details.Number}");
            Trace.WriteLine($"   >> Статус: {details.StatusTitle} ({details.Status})");
            Trace.WriteLine($"   >> Рассчитанная длина маршрута: {details.RouteLenght}");
            Trace.WriteLine($"   >> Количество предложений: {details.ProposalsCount}");
            if (details.BestProposal != null)
                Trace.WriteLine($"   >> Лучшее предложение: {details.BestProposal.Bet}");

            return details;
        }

        /// <summary>
        /// Метод очистки класса (исполняется один раз после всех тестов).
        /// Осуществляет удаление глобального объекта клиента.
        /// </summary>
        [ClassCleanup]
        public static void Cleanup()
        {
            Client.Dispose();
        }
    }
}
