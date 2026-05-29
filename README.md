# System Monitor Agent

Windows служба для мониторинга системы (CPU, RAM, диски, процессы).

## Сборка проекта
```
dotnet publish -c Release -o publish
```

## Установка службы (запускать от имени администратора)
```
sc create "SystemMonitorAgent" binPath="C:\путь\к\publish\SystemMonitorAgent.exe" start=auto
```

## Управление службой

Запуск
```
net start SystemMonitorAgent
```

Остановка
```
net stop SystemMonitorAgent
```

Удаление
```
sc delete SystemMonitorAgent
```

## Настройка
Отредактируйте appsettings.json в папке со службой:
```
{
  "AppSettings": {
    "ApiUrl": "http://62.173.152.161:8099/ok",
    "Interval": 30,
    "Proc2Monitor": [ "v2rayN", "steam", "nbase", "nbservice" ],
    "LogFileName": "C:/temp/sysmon/system_monitor_agent.log",
    "QueueFolder": "C:/temp/sysmon/queue",
    "ApiTimeout": 10
  }
}
```
После изменения конфигурации: перезапустите службу.

Для теста можно использовать следующие url:

| URL | поведение |
|-----------|-----------|
| http://62.173.152.161:8099/ok | мгновенный успешный ответ |
| http://62.173.152.161:8099/slow | успешный ответ с задержкой от 2 до 12 сек |
| http://62.173.152.161:8099/hang | успешный ответ через 5 минут |
| http://62.173.152.161:8099/buggy | в 50% запросов возвращает какую-то ошибку |

## Проверка работы
* Проверьте файл лога (путь из LogFileName)
* или найдите службу в списке служб
* или выполните sc query SystemMonitorAgent

## Логи
JSON лог: задаётся в настройках либо system_monitor_agent.log в текущем каталоге приложения

Get-EventLog -LogName Application -Source "SystemMonitorAgent" -Newest 20