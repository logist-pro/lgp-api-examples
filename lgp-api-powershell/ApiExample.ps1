# базовые настройки доступа
$API_BASE_HOST = "http://..."
$API_BASE_URL = $API_BASE_HOST+"/api/v1/"
# API ключ личного кабинета компании
$API_KEY = "..."
# логин/пароль технического аккаунта с максимальными правами доступа
$LOGIN = "..."
$PASSWORD = "..."

# выход при возникновении любого исключения с выводом ошибки
trap 
{ 
    Write-Error $_ 
    exit
}

# подготовка базовых заголовков для HTTP запросов
$headers = @{}
$headers.Add("X-ApiKey", $API_KEY)
# явное указание протокола при использовании TLS/SSL соединения
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

  
# -----
# 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
# -----
$url = "$($API_BASE_URL)test/ping"
Write-Host "1. Проверка доступа к API:" $url
$_ = Invoke-RestMethod -Uri $url -Headers $headers
Write-Host "   >> есть доступ"


# -----
# 2. авторизация в системе заданным пользователем
# -----
$url = "$($API_BASE_URL)account/login"
Write-Host "2. Авторизация:" $url
# передача параметров в URL
$url = "$($url)?login=$($LOGIN)&password=$($PASSWORD)"
# POST запрос с пустым телом 
# (используем Invoke-WebRequest, чтобы получить заголовки ответа)
$response = Invoke-WebRequest -Uri $url -Method Post -Body "" -Headers $headers
Write-Host "   >> прошла успешно"
# поиск Set-Cookie заголовка ответа
$appCookie = $response.Headers["Set-Cookie"]
if (!$appCookie) {
    Write-Error "[Error] cookie не установлена"
    exit
}
# значения первой возвращенной куки (.AspNet.ApplicationCookie)
# добавляется в вэбсессию для всех последующих HTTP запросов
$сookie = New-Object System.Net.Cookie
$сookie.Name = ".AspNet.ApplicationCookie"
$сookie.Value = $appCookie.split(';')[0].split('=')[1]
$сookie.Domain = ([System.Uri]$url).DnsSafeHost
$webSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$webSession.Cookies.Add($сookie)


# -----
# 3. перед созданием нового запроса, необходимо определить значения некоторых обязательных параметров
#    (в частности, идентификаторов компании заказчика и ответственного за заказ лица)
#    если эти параметры неизвестны, то можно запросить актуальные справочники с сервера
# -----
$url = "$($API_BASE_URL)tender/create"
Write-Host "3. Получение справочников для создания запроса:" $url
$dicts = Invoke-RestMethod -Uri $url -Headers $headers -WebSession $webSession
$corporateId = $dicts.Corporates[0].Id;
$contactId = $dicts.Corporates[0].ContactPersons[0].Id;
if (!$corporateId -Or !$contactId) {
    Write-Error "[Error] не найдены необходимые элементы справочника"
    exit
}


# -----
# 4. Создание запроса
# -----
Write-Host "4. Создание запроса:" $url
# время создания заказа
$now = Get-Date;
# дата исполнения заказа (текущая дата +7 дней, 08:00)
$start = $now.Date.AddDays(7).AddHours(8);
# объект для передачи в запросе в формате JSON
$tender = @{
    # время создания заказа (может быть любая прошедшая дата)
    OrderDate = $now.ToString("O")
    # дата исполнения заказа
    StartDate = $start.ToString("O")
    Customer = @{
        CompanyId = $corporateId
        ContactId = $contactId
    }
    Cargo = "Важный груз"
    CargoWeight = 10
    CargoVolume = 10
    CargoDangerClass = 0
    # сборный груз (не требует детального описания упаковки)
    PackageType = "Joint"
    RoutePoints = 
        @{
            # точка погрузки
            Type = "Loading"
            Address = "Москва, Красная площадь"
            # Address = "Москва"
            # время прибытия = дата исполнения заказа
            ArrivalTime = $start.ToString("O")
            # время отбытия +1 час
            LeaveTime = $start.AddHours(1).ToString("O")
        },
        @{
            # точка выгрузки
            Type = "Unloading"
            Address = "Санкт-Петербург, Дворцовая площадь"
            # Address = "Санкт-Петербург"
            # если не указывть расстояние до точки маршрута, то система попытается самостоятельно построить маршрут и определить расстояние
            # "Distance": 750,
            ArrivalTime = $start.AddHours(10).ToString("O")
            LeaveTime = $start.AddHours(11).ToString("O")
        }
    Tender = @{
        # запуск торгов сразу по созданию заказа
        StartDate = $now.ToString("O")
        # торги должны закончиться минимум за 30 минут до исполнения заказа
        EndDate = $start.AddHours(-1).ToString("O")
        InitCost = 50000
        # торги с автоматическим подбором минимального шага
        MinStepReq = "Auto"
        VatReqs = "None"
    }
}
# конвертация в JSON
$body = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $tender))
# параметр -ContentType указывает на формат передаваемого тела запроса
$result = Invoke-RestMethod `
    -Uri $url -Method "Post" `
    -ContentType "application/json; charset=utf-8" -Body $body `
    -Headers $headers -WebSession $webSession
$tenderId = $result.Trim('"');
if (!$tenderId) {
    Write-Error "[Error] не получен идентификатор нового запроса"
    exit
}
Write-Host "   >> Создан запрос Id = " $tenderId


# -----
# 5. для проверки статуса нового запроса, запрашиваем полные данные по идентификатору
# -----
$url = "$($API_BASE_URL)tender/$($tenderId)"
Write-Host "5. Проверка статуса запроса:" $url
$tender = Invoke-RestMethod -Uri $url -Headers $headers -WebSession $webSession
if (!$tender) {
    Write-Error "[Error] не получена детальная информация нового запроса"
    exit
}
Write-Host "   >> Номер:" $tender.Number
Write-Host "   >> Статус:" $tender.StatusTitle "($($tender.Status))"
Write-Host "   >> Время последнего изменения статуса:" $tender.ActualDateTitle "($($tender.ActualDate))"
Write-Host "   >> Длина маршрута:" $tender.RouteLenght
Write-Host "   >> Количество предложений:" $tender.ProposalsCount
if ($tender.BestProposal) {
    Write-Host "   >> Лучшее предложение:" $tender.BestProposal.Bet
}