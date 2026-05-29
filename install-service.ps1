<#
.SYNOPSIS
Устанавливает, обновляет или удаляет Windows службу SystemMonitorAgent.
.DESCRIPTION
Скрипт автоматизирует развертывание службы: публикацию проекта, создание директорий,
установку/переустановку службы и настройку прав на запись логов.
.PARAMETER Uninstall
Полностью удаляет службу и останавливает её, если она запущена.
.PARAMETER Reinstall
Останавливает, удаляет, а затем заново устанавливает и запускает службу.
.EXAMPLE
.\install-service.ps1           # Стандартная установка
.\install-service.ps1 -Reinstall # Переустановка службы
.\install-service.ps1 -Uninstall # Удаление службы
#>

param (
    [switch]$Uninstall,
    [switch]$Reinstall
)

$ServiceName = "SystemMonitorAgent"
$PublishDir = Join-Path $PSScriptRoot "SystemMonitorAgent\publish"
$ServiceBinPath = Join-Path $PublishDir "SystemMonitorAgent.exe"
$ConfigPath = Join-Path $PublishDir "appsettings.json"

Write-Host "PSScriptRoot: $PSScriptRoot"

# Запрос прав администратора
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "Этот скрипт требует прав администратора. Перезапустите PowerShell от имени администратора." -ForegroundColor Red
    exit 1
}

# Остановка и удаление службы
function Remove-ServiceIfExists {
    if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Останавливаем службу '$ServiceName'..." -ForegroundColor Yellow
        Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
        Write-Host "Удаляем службу '$ServiceName'..." -ForegroundColor Yellow
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Служба '$ServiceName' не найдена." -ForegroundColor Cyan
    }
}

function Initialize-Directory {
	if (-NOT (Test-Path $PublishDir)) {
		New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
		Write-Host "Создана директория: $PublishDir" -ForegroundColor Green
	}
}

# --- Основная логика ---

if ($Uninstall) {
    Write-Host "Начинаем удаление службы '$ServiceName'..." -ForegroundColor Yellow
    Remove-ServiceIfExists
    Write-Host "Служба '$ServiceName' успешно удалена." -ForegroundColor Green
    exit 0
}

if ($Reinstall) {
    Write-Host "Переустановка службы '$ServiceName'..." -ForegroundColor Yellow
    Remove-ServiceIfExists
}


Write-Host "Публикуем проект в '$PublishDir'..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\SystemMonitorAgent\SystemMonitorAgent.csproj" -c Release -o "$PublishDir" --no-self-contained
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка при публикации проекта. Проверьте, что dotnet CLI установлен и путь к проекту верен." -ForegroundColor Red
    exit $LASTEXITCODE
}

Initialize-Directory

# Установка прав на папку с логами
$logDir = Split-Path (Get-Content $ConfigPath | ConvertFrom-Json).AppSettings.LogFileName -Parent
if (Test-Path $logDir) {
    icacls $logDir /grant "LOCAL SERVICE:(OI)(CI)F" /T /Q
    Write-Host "Установлены права на запись для LOCAL SERVICE в '$logDir'" -ForegroundColor Green
}

# Создание службы
Write-Host "Создаём службу '$ServiceName'..." -ForegroundColor Yellow
New-Service -Name $ServiceName `
            -BinaryPathName $ServiceBinPath `
            -DisplayName "System Monitor Agent" `
            -Description "Служба мониторинга системы (CPU, RAM, диски, процессы)" `
            -StartupType Automatic `
            -ErrorAction Stop

# Запуск службы
Write-Host "Запускаем службу '$ServiceName'..." -ForegroundColor Yellow
Start-Service $ServiceName

# Проверка статуса
$service = Get-Service $ServiceName
if ($service.Status -eq 'Running') {
    Write-Host "Служба '$ServiceName' успешно установлена и запущена!" -ForegroundColor Green
    Write-Host "Логи службы можно посмотреть командой: Get-EventLog -LogName Application -Source 'SystemMonitorAgent' -Newest 20" -ForegroundColor Cyan
} else {
    Write-Host "Служба установлена, но не запущена. Текущий статус: $($service.Status)" -ForegroundColor Yellow
}