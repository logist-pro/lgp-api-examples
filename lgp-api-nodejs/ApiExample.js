const fetch = require('node-fetch');

// базовые настройки доступа
const API_BASE_HOST = "https://...";
const API_BASE_URL = API_BASE_HOST+"/api/v1/";
// API ключ личного кабинета компании
const API_KEY = "...";
// логин/пароль технического аккаунта с максимальными правами доступа
const LOGIN = "...";
const PASSWORD = "...";


// подготовка базовых заголовков для HTTP запросов
const headers = {
  'Accept': 'application/json',
  'Content-Type': 'application/json',
  'X-ApiKey': API_KEY,
};

// запуск тестовой последовательности
main();


async function main() {

  // -----
  // 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
  // -----
  let url = API_BASE_URL+'test/ping';
  console.log('1. Проверка доступа к API: ', url);
  // GET запрос на сервер
  let response = await requestGet(url);
  if (!response) {
    console.log('   >> API не доступен');
    // выход из примера
    return;
  }
  console.log('   >> есть доступ');
  

  // -----
  // 2. авторизация в системе заданным пользователем
  // -----
  url = API_BASE_URL+'account/login';
  console.log('2. Авторизация: ', url);
  // передача параметров в URL
  url = url+`?login=${LOGIN}&password=${PASSWORD}`;
  // POST запрос с пустым телом
  response = await requestPost(url);
  if (!response) return;
  console.log('   >> прошла успешно');
  // поиск Set-Cookie заголовка ответа
  const cookie = response.headers.get('Set-Cookie');
  if (!cookie) {
    console.log('   >> [Error] cookie не установлена');
    return;
  }
  // значения первой возвращенной куки (.AspNet.ApplicationCookie)
  // добавляется в заголовки всех последующих HTTP запросов
  headers['Cookie'] = cookie.split(';')[0];


  // -----
  // 3. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
  //    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
  //    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера
  // -----
  url = API_BASE_URL+'tender/create';
  console.log('3. Получение справочников для создания запроса: ', url);
  response = await requestGet(url);
  if (!response) return;
  // разбор JSON ответа сервера
  const dicts = await response.json();
  // получение идентификаторов из первых элементов справочников
  const corporateId = dicts.Corporates[0].Id;
  const contactId = dicts.Corporates[0].ContactPersons[0].Id;
  if (!corporateId || !contactId) {
    console.log('   >> [Error] не найдены необходимые элементы справочника');
    return;
  }


  // -----
  // 4. Создание запроса
  // -----
  console.log('4. Создание запроса: ', url);
  // время создания заказа
  const now = new Date();
  // дата исполнения заказа (текущая дата +7 дней, 08:00)
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate()+7, 8, 0);
  // объект для передачи в запросе в формате JSON
  let tender = {
    // время создания заказа (может быть любая прошедшая дата)
    "OrderDate": now.toISOString(),
    // дата исполнения заказа
    "StartDate": start.toISOString(),
    "Customer": {
      "CompanyId": corporateId,
      "ContactId": contactId
    },
    "Cargo": "Важный груз",
    "CargoWeight": 10,
    "CargoVolume": 10,
    "CargoDangerClass": 0,
    // сборный груз (не требует детального описания упаковки)
    "PackageType": "Joint",
    "RoutePoints": [
      {
        // точка погрузки
        "Type": "Loading",
        "Address": "Москва, Красная площадь",
        // время прибытия = дата исполнения заказа
        "ArrivalTime": start.toISOString(),
        // время отбытия +1 час
        "LeaveTime": new Date((new Date(start)).setHours(9)).toISOString(),
      },
      {
        // точка выгрузки
        "Type": "Unloading",
        "Address": "Санкт-Петербург, Дворцовая площадь",
        // если не указывть расстояние до точки маршрута, то система попытается самостоятельно построить маршрут и определить расстояние
        // "Distance": 750,
        "ArrivalTime": new Date((new Date(start)).setHours(19)).toISOString(),
        "LeaveTime": new Date((new Date(start)).setHours(20)).toISOString()
      }
    ],
    "Tender": {
      // запуск торгов сразу по созданию заказа
      "StartDate": now.toISOString(),
      // торги должны закончиться минимум за 30 минут до исполнения заказа
      "EndDate": new Date((new Date(start)).setHours(7)).toISOString(),
      "InitCost": 50000,
      // торги с автоматическим подбором минимального шага
      "MinStepReq": "Auto",
      "VatReqs": "None",
    }
  };
  response = await requestPost(url, tender);
  if (!response) return;
  // получение идентификатора новосозданного запроса
  const tenderId = (await response.text()).replace(/"/g,'');
  if (!tenderId) {
    console.log('   >> [Error] не получен идентификатор нового запроса');
    return;
  }
  console.log('   >> Создан запрос Id=', tenderId);
  
  
  // -----
  // 5. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
  // -----
  url = API_BASE_URL+'tender/'+tenderId;
  console.log('5. Проверка статуса запроса: ', url);
  response = await requestGet(url);
  if (!response) return;
  tender = await response.json();
  console.log('   >> Номер: ', tender.Number);
  console.log(`   >> Статус: ${tender.StatusTitle} (${tender.Status})`);
  console.log(`   >> Время последнего изменения статуса: ${tender.ActualDate} (${tender.ActualDateTitle})`);
  console.log('   >> Длина маршрута: ', tender.RouteLenght);
  console.log('   >> Количество предложений: ', tender.ProposalsCount);
  if (tender.BestProposal)
    console.log('   >> Лучшее предложение: ', tender.BestProposal.Bet);
}

// обработка результатов запроса
function handleErrors(response) {
  // если ошибка, то выбрасывается исключение
  if (!response.ok) {
    response.text().then(console.error);
    throw Error(`[${response.status}] ${response.statusText}`);
  }
  // если ошибок нет, то ответ сервера спускается дальше
  return response;
}
function requestGet(url) {
  return request(url, undefined, 'get');
}
function requestPost(url, body) {
  return request(url, body, 'post');
}
function request(url, body, method) {
  return fetch(url, { 
    method,
    headers,
    body: body && JSON.stringify(body),
  })
  .then(handleErrors)
  .catch(console.error);
}
