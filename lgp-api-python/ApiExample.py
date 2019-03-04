import sys
import datetime
import json
import requests

# базовые настройки доступа
API_BASE_HOST = "https://..."
API_BASE_URL = API_BASE_HOST+"/api/v1/"
# API ключ личного кабинета компании
API_KEY = "..."
# логин/пароль технического аккаунта с максимальными правами доступа
LOGIN = "..."
PASSWORD = "..."

# функция проверки результатов запроса по коду ошибки и вывода детальной информации
def handleErrors(response):
  if response.status_code != 200:
    message = response.content.decode('utf-8')
    print('   >> [Error] HttpCode={}:\n{}'.format(response.status_code, message))
    return response.status_code


# подготовка базовых заголовков для HTTP запросов
headers = {
  'Accept': 'application/json',
  'Content-Type': 'application/json',
  'X-ApiKey': API_KEY,
}


# -----
# 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
# -----
url = API_BASE_URL+'test/ping'
print('1. Проверка доступа к API: {}'.format(url))
# GET запрос на сервер
response = requests.get(url, headers=headers)
# проверка ответа сервера
if handleErrors(response):
  print('   >> API не доступен')
  # выход с отрицательным кодом ошибки
  sys.exit(-1)
else: print('   >> есть доступ')


# -----
# 2. авторизация в системе заданным пользователем
# -----
url = API_BASE_URL+'account/login'
print('2. Авторизация: {}'.format(url))
# передача параметров в URL
url = url+'?login={}&password={}'.format(LOGIN, PASSWORD)
# POST запрос с пустым телом
response = requests.post(url, headers=headers)
if handleErrors(response): sys.exit(-2)
else: 
  print('   >> прошла успешно')
  # поиск Set-Cookie заголовка ответа
  cookie = response.headers.get('Set-Cookie')
  if cookie is None:
    print('   >> [Error] cookie не установлена')
    sys.exit(-3)
  # значения первой возвращенной куки (.AspNet.ApplicationCookie)
  # добавляется в заголовки всех последующих HTTP запросов
  headers['Cookie'] = cookie.split(';')[0]


# -----
# 3. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
#    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
#    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера
# -----
url = API_BASE_URL+'tender/create'
print('3. Получение справочников для создания запроса: {}'.format(url))
response = requests.get(url, headers=headers)
if handleErrors(response): sys.exit(-4)
else:
  # разбор JSON ответа сервера
  dicts = json.loads(response.content.decode('utf-8'))
  # получение идентификаторов из первых элементов справочников
  coprorateId = dicts['Corporates'][0]['Id']
  contactId = dicts['Corporates'][0]['ContactPersons'][0]['Id']
  if coprorateId is None or contactId is None:
    print('   >> [Error] не найдены необходимые элементы справочника.')
    sys.exit(-5)


# -----
# 4. Создание запроса
# -----
print('4. Создание запроса: {}'.format(url))
# время создания заказа
now = datetime.datetime.now()
# дата исполнения заказа (текущая дата +7 дней, 08:00)
start = now.combine(now.replace(day=now.day+7), datetime.time(8,0))
# формат передачи даты/времени в запросах
DATETIME_FORMAT = '%F %H:%M'
# объект для передачи в запросе в формате JSON
tender = {
  # время создания заказа (может быть любая прошедшая дата)
  "OrderDate": now.strftime(DATETIME_FORMAT),
  # дата исполнения заказа
  "StartDate": start.strftime(DATETIME_FORMAT),
  "Customer": {
    "CompanyId": coprorateId,
    "ContactId": contactId
  },
  "Cargo": "Важный груз",
  "CargoWeight": 10,
  "CargoVolume": 10,
  "CargoDangerClass": 0,
  # сборный груз (не требует детального описания упаковки)
  "PackageType": "Joint",
  "RoutePoints": [
    {
      # точка погрузки
      "Type": "Loading",
      "Address": "Москва, Красная площадь",
      # время прибытия = дата исполнения заказа
      "ArrivalTime": start.strftime(DATETIME_FORMAT),
      # время отбытия +1 час
      "LeaveTime": start.replace(hour=9).strftime(DATETIME_FORMAT)
    },
    {
      # точка выгрузки
      "Type": "Unloading",
      "Address": "Санкт-Петербург, Дворцовая площадь",
      # если не указывть расстояние до точки маршрута, то система попытается самостоятельно построить маршрут и определить расстояние
      # "Distance": 750,
      "ArrivalTime": start.replace(hour=19).strftime(DATETIME_FORMAT),
      "LeaveTime": start.replace(hour=20).strftime(DATETIME_FORMAT)
    }
  ],
  "Tender": {
    # запуск торгов сразу по созданию заказа
    "StartDate": now.strftime(DATETIME_FORMAT),
    # торги должны закончиться минимум за 30 минут до исполнения заказа
    "EndDate": start.replace(hour=7).strftime(DATETIME_FORMAT),
    "InitCost": 50000,
    # торги с автоматическим подбором минимального шага
    "MinStepReq": "Auto",
    "VatReqs": "None",
  }
}
response = requests.post(url, headers=headers, json=tender)
if handleErrors(response): sys.exit(-6)
else:
  # получение идентификатора новосозданного запроса
  tenderId = response.content.decode('utf-8').strip('"')
  print('   >> Создан запрос Id={}'.format(tenderId))


# -----
# 5. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
# -----
url = API_BASE_URL+'tender/'+tenderId
print('5. Проверка статуса запроса: {}'.format(url))
response = requests.get(url, headers=headers)
if handleErrors(response): sys.exit(-7)
else:
  tender = json.loads(response.content.decode('utf-8'))
  print('   >> Номер: {}'.format(tender['Number']))
  print('   >> Статус: {} ({})'.format(tender['StatusTitle'], tender['Status']))
  print('   >> Время последнего изменения статуса: {} ({})'.format(tender['ActualDate'], tender['ActualDateTitle']))
  print('   >> Длина маршрута: {}'.format(tender['RouteLenght']))
  print('   >> Количество предложений: {}'.format(tender['ProposalsCount']))
  if tender['BestProposal'] is not None:
    print('   >> Лучшее предложение: {}'.format(tender['BestProposal']['Bet']))