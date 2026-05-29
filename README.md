# System Monitor Agent

Windows служба для мониторинга системы (CPU, RAM, диски, процессы).

## Сборка проекта
```
powershell

dotnet publish -c Release -o publish
```

## Установка службы (запускать от имени администратора)
```
powershell

sc create "SystemMonitorAgent" binPath="C:\путь\к\publish\SystemMonitorAgent.exe" start=auto
```

## Автоматическая установка через PowerShell

Скрипт `install-service.ps1` автоматически публикует проект, создаёт директории, устанавливает и запускает службу.

**Запуск от имени администратора:**
```
powershell

.\install-service.ps1      # Стандартная установка
.\install-service.ps1 -Reinstall  # Переустановка
.\install-service.ps1 -Uninstall  # Удаление службы
```

## Управление службой

Запуск
```
powershell

net start SystemMonitorAgent
```

Остановка
```
powershell

net stop SystemMonitorAgent
```

Удаление
```
powershell

sc delete SystemMonitorAgent
```

## Настройка
Отредактируйте appsettings.json в папке со службой:
```
json

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

```
powershell

Get-EventLog -LogName Application -Source "SystemMonitorAgent" -Newest 20
```

## Пример объекта, передаваемого на сервер

```
json

{
  "hostname": "BAND-PC",
  "ip": [
    "192.168.56.1",
    "192.168.1.2",
    "127.0.0.1"
  ],
  "version": "Windows 10 Pro (2009)",
  "uptime": "8.08:20:53.5460000",
  "cpu": [
    8,
    7.5,
    13.7,
    4.4,
    7.5,
    1.3
  ],
  "ram": {
    "total_mb": 24486,
    "used_mb": 17554,
    "percentage": 71.68995
  },
  "free_space_gb": {
    "C:": 18,
    "D:": 87,
    "E:": 0,
    "G:": 43,
    "H:": 21
  },
  "all_processes": [
    "ai",
    "aimgr",
    "ApplicationFrameHost",
    "AppVShNotify",
    "audiodg",
    "CDASrv",
    "chrome",
    "cmd",
    "Code",
    "CompPkgSrv",
    "conhost",
    "crashpad_handler",
    "csp4service",
    "csrss",
    "ctfmon",
    "dasHost",
    "devenv",
    "DevHub",
    "dllhost",
    "dwm",
    "explorer",
    "FileCoAuth",
    "fontdrvhost",
    "Idle",
    "IntegrityService",
    "lghub_agent",
    "lghub_system_tray",
    "lghub_updater",
    "LockApp",
    "logioptionsplus_updater",
    "LsaIso",
    "lsass",
    "max",
    "max-service",
    "Memory Compression",
    "MigrationService",
    "mmc",
    "MoUsoCoreWorker",
    "MpDefenderCoreService",
    "ms-teams",
    "MSBuild",
    "msedge",
    "msedgewebview2",
    "MsMpEng",
    "net",
    "net1",
    "NisSrv",
    "node",
    "notepad\u002B\u002B",
    "nvcontainer",
    "NVDisplay.Container",
    "NVIDIA Overlay",
    "nvsphelper64",
    "OfficeClickToRun",
    "OneDrive",
    "OneDrive.Sync.Service",
    "OpenConsole",
    "OutlineService",
    "PerfWatson2",
    "pet",
    "pg_ctl",
    "postgres",
    "powershell",
    "redirector",
    "Registry",
    "RtkAudUService64",
    "rudesktop",
    "RuntimeBroker",
    "SamsungMagicianSVC",
    "SearchApp",
    "SearchIndexer",
    "Secure System",
    "SecurityHealthService",
    "SecurityHealthSystray",
    "service_update",
    "ServiceHub.IntellicodeModelService",
    "services",
    "ShellExperienceHost",
    "sihost",
    "smartscreen",
    "smss",
    "spdsvc",
    "spoolsv",
    "sppsvc",
    "sqlceip",
    "sqlservr",
    "sqlwriter",
    "ssh",
    "Ssms",
    "StartMenuExperienceHost",
    "steam",
    "steamservice",
    "steamwebhelper",
    "svchost",
    "symsrvhost",
    "System",
    "SystemMonitorAgent",
    "SystemSettings",
    "taskhostw",
    "Taskmgr",
    "Telegram",
    "TextInputHost",
    "TGitCache",
    "TSVNCache",
    "unsecapp",
    "UpdaterService",
    "UserOOBEBroker",
    "v2rayN",
    "VBCSCompiler",
    "vpnagent",
    "VpnClient",
    "VpnService",
    "WDDriveService",
    "WDDriveUtilitiesHelper",
    "WhatsApp.Root",
    "WindowsTerminal",
    "wininit",
    "winlogon",
    "WINWORD",
    "WmiPrvSE",
    "wslservice",
    "WUDFHost",
    "xray",
    "YandexTelemostUpdateService"
  ],
  "monitored_processes": {
    "v2rayN": true,
    "steam": true,
    "nbase": false,
    "nbservice": false
  },
  "timestamp": "2026-05-26T19:39:40.4758134+03:00",
  "totalcpu": 7.2
}
```
