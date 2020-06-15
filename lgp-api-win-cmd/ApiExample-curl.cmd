@ECHO OFF
chcp 65001

REM базовые настройки доступа
SET API_BASE_HOST=https://...
SET API_BASE_URL=%API_BASE_HOST%/api/v1
REM API ключ личного кабинета компании
SET API_KEY=...
REM "логин/пароль технического аккаунта с максимальными правами доступа"
SET LOGIN=...
SET PASSWORD=...

SET CURL=d:\Distrib\curl\bin\curl.exe --fail
REM параметры для сохранения и загрузки cookie
SET SAVE_COOKIE=--cookie-jar "cookie.txt"
SET LOAD_COOKIE=--cookie "cookie.txt"

REM базовые заголовки HTTP запроса (передача ключа API и ожидание ответа в формате JSON)
SET HEADER=--header "X-ApiKey: %API_KEY%" --header "Accept: application/json; charset=utf-8"


REM -----
REM 1. тестирование доступа (запрос не требует авторизации, только ключ API в заголовках запроса)
REM -----
SET URL="%API_BASE_URL%/test/ping"
ECHO 1. Проверка доступа к API: %URL%
%CURL% -X GET %HEADER% %URL%
IF %ERRORLEVEL% NEQ 0 Exit /B %ERRORLEVEL%
ECHO    есть доступ


REM -----
REM 2. авторизация в системе заданным пользователем
REM -----
SET URL="%API_BASE_URL%/account/login"
ECHO 2. Авторизация: %URL%
%CURL% -X POST --data "{Login:^"%LOGIN%^",Password:^"%PASSWORD%^"}" %HEADER% %SAVE_COOKIE% %URL%
ECHO    прошла успешно


REM -----
REM 3. получение списка актуальных заявок
REM -----
SET URL="%API_BASE_URL%/request"
ECHO 3. Список актуальных заявок: %URL%
%CURL% -X GET %HEADER% %LOAD_COOKIE% %URL%
