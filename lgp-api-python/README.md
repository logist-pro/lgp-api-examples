# Пример использования API на Python 3

Полная документация API https://wiki.logistpro.su/display/LKB/LogistPro+API

## Запуск примера
Для запуска примера, необходимо:
1. установить [Python 3](https://www.python.org/downloads/) для Windows, Linux или macOS
2. установить библиотеку [requests](https://pypi.org/project/requests/) 
	```
	pip install requests
	```
3. заполнить необходимые константы в файле [ApiExample.py](ApiExample.py)
	- базовые настройки доступа (актуализировать базовые настройки можно в [Wiki](https://wiki.logistpro.su/display/LKB/LogistPro+API) проекта)
	```python
	# базовые настройки доступа
	API_BASE_HOST = "https://..."
	API_BASE_URL = API_BASE_HOST+"/api/v1/"
	```
	- API ключ личного кабинета компании (запросить ключ можно обратившись в [Службу поддержки](https://jira.logistpro.su/servicedesk/customer/portal/4) или написав заявку по адресу support@logistpro.su)
	```python
	# API ключ личного кабинета компании
	API_KEY = "..."
	```
	- логин/пароль технического аккаунта (можно использовать любой логин/пароль пользователя из личного кабинета компании)
	```python
	# логин/пароль технического аккаунта с максимальными правами доступа
	LOGIN = "..."
	PASSWORD = "..."
	```
3. запустить пример командой
	```
	python ApiExample.py
	```

## Сценарий примера
Сценарий реализован в виде последовательности запросов к API, реализующих функции создания и контроля статуса заказа.

0. Настройка передачи API ключа в заголовках HTTP запросов
1. Проверка доступности API
2. Авторизация заданным пользователем
	- получение авторизационной куки
	- настройка передачи авторизации в заголовках HTTP запросов
3. Получение справочников с сервера
4. Создание запроса на транспорт (с датой исполнения +7 дней вперед)
5. Проверка статуса созданного запроса с получением дополнительной информации:
	- Статус с расшифровкой
	- Время последнего изменения статуса
	- Количество предложений
	- Текущее лучшее предложение