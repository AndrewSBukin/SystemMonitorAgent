using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime;
using System.ServiceProcess;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SystemMonitorAgent
{
    public class AppSettings
    {
        public string ApiUrl { get; set; } = @"http://62.173.152.161:8099/ok";
        public int Interval { get; set; } = 30;
        public string LogFileName { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system_monitor_agent.log");
        public List<string> Proc2Monitor { get; set; } = new();
        public int ApiTimeout { get; set; } = 10;
        public string QueueFolder { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QueueData");
    }

    internal class Program
    {
        public static string s_ErrorDuring_Configure = "";
        static void Main(string[] args)
        {
            // TODO: защититься от повторного запуска

            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;
                    var settings = config.GetSection("AppSettings").Get<AppSettings>();
                    if(settings == null)
                    {
                        settings = new AppSettings();
                        s_ErrorDuring_Configure = "Couldn't locate settings file. Using default settings.";
                    }
                    services.AddSingleton(settings);
                    services.AddSingleton<Logger>();
                    services.AddHostedService<SystemMonitorAgent>();
                    services.AddHttpClient<DataSender>((sp, client) =>
                    {
                        var settings = sp.GetRequiredService<AppSettings>();
                        client.Timeout = TimeSpan.FromSeconds(settings.ApiTimeout);
                    });
                })
                .Build()
                .Run();
        }
    }


    public class Logger
    {
        private readonly string _logFileName;
        private readonly string _logDirectory;

        public Logger(AppSettings settings)
        {
            // TODO: проверить корректность пути
            _logFileName = settings.LogFileName;
            _logDirectory = Path.GetDirectoryName(_logFileName);
            Directory.CreateDirectory(_logDirectory);

            if (!EventLog.SourceExists("SystemMonitorAgent"))
                EventLog.CreateEventSource("SystemMonitorAgent", "Application");
        }

        public void Log(string message, bool isError = false)
        {
            // TODO: обработать ошибки
            try
            {
                if (!string.IsNullOrEmpty(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);
                var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(_logFileName, logLine);
            }
            catch (Exception ex)
            {
            }

            try
            {
                using (EventLog eventLog = new EventLog("Application"))
                {
                    eventLog.Source = "SystemMonitorAgent";
                    eventLog.WriteEntry(message, isError? EventLogEntryType.Error: EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                // больше писать некуда.
            }
        }
    }

    public class DataSender
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _settings;
        private readonly Logger _logger;
        private readonly FileQueue _queue;
        private bool _isProcessing = false;

        public DataSender(HttpClient httpClient, AppSettings settings, Logger logger)
        {
            _httpClient = httpClient;
            _settings = settings;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.ApiTimeout);
            _queue = new FileQueue(_settings, _logger);
        }

        public async Task SendDataAsync(string jsonData)
        {
            await _queue.EnqueueAsync(jsonData);
            await ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            if (_isProcessing) return;

            _isProcessing = true;

            try
            {
                string? data;
                while ((data = await _queue.DequeueAsync()) != null)
                {
                    if (await TrySendAsync(data))
                    {
                        await _queue.DequeueDeleteAsync();
                    }
                    else
                        break;
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private async Task<bool> TrySendAsync(string jsonData)
        {
            try
            {
                var response = await _httpClient.PostAsync(_settings.ApiUrl, new StringContent(jsonData));
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _logger.Log($"Данные успешно отправлены");
                    return true;
                }
                else
                    _logger.Log($"Ошибка при отправке: {response.StatusCode}", true);
            }
            catch (Exception ex)
            {
                _logger.Log($"Ошибка при отправке: {ex.Message}", true);
            }
            return false;
        }
    }

    public class SystemMonitorAgent : BackgroundService
    {
        private readonly AppSettings _settings;
        private readonly Logger _logger;
        private readonly DataSender _poster;

        public SystemMonitorAgent(AppSettings settings, Logger logger, DataSender poster)
        {
            _settings = settings;
            _logger = logger;
            _poster = poster;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Log("Служба запущена");
            var collector = new SystemCollector();
            var processes2Monitor = _settings.Proc2Monitor ?? new List<string>();

            if(Program.s_ErrorDuring_Configure != "")
            {
                _logger.Log(Program.s_ErrorDuring_Configure, true);
                Program.s_ErrorDuring_Configure = "";
            }
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var sysInfo = new SysInfo
                    {
                        hostname = Environment.MachineName,
                        ip = GetIpAddresses(),
                        version = GetWindowsVersion(),
                        uptime = GetUptime(),
                        cpu = await collector.GetCpuUsagePerCore(),
                        totalcpu = await collector.GetTotalCpuUsage(),
                        ram = GetRamUsage(),
                        free_space_gb = GetDisksFreeSpace(),
                        timestamp = DateTime.Now
                    };
                    (Dictionary<string, bool> RequiredProcesses, List<string> AllProcesses) = CheckProcesses(processes2Monitor);
                    sysInfo.all_processes = AllProcesses;
                    sysInfo.monitored_processes = RequiredProcesses;

                    var json = JsonSerializer.Serialize(sysInfo, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    //_logger.Log(json);
                    _poster.SendDataAsync(json);

                    await Task.Delay(TimeSpan.FromSeconds(_settings.Interval), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {

            }
            _logger.Log("Служба остановлена");
        }

        #region Model
        public class SysInfo
        {
            public string hostname { get; set; }
            public List<string> ip { get; set; }
            public string version { get; set; }
            public TimeSpan uptime { get; set; }
            public List<double> cpu { get; set; }
            public RamInfo ram { get; set; }
            public Dictionary<string, long> free_space_gb { get; set; }
            public List<string> all_processes { get; set; }
            public Dictionary<string, bool> monitored_processes { get; set; }
            public DateTime timestamp { get; internal set; }
            public double totalcpu { get; internal set; }
        }
        public class RamInfo
        {
            public long total_mb { get; set; }
            public long used_mb { get; set; }
            public double percentage { get; set; }
        }
        #endregion

        #region GetInfo
        private List<string> GetIpAddresses()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.Address.ToString())
                .ToList();
        }
        private string GetWindowsVersion()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                return $"{key?.GetValue("ProductName")} ({key?.GetValue("ReleaseId")})";
            }
        }
        private TimeSpan GetUptime()
        {
            return TimeSpan.FromMilliseconds(Environment.TickCount64);
        }
        private RamInfo GetRamUsage()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalMB = Convert.ToInt64(obj["TotalVisibleMemorySize"]) / 1024;
                    var freeMB = Convert.ToInt64(obj["FreePhysicalMemory"]) / 1024;
                    var usedMB = totalMB - freeMB;

                    return new RamInfo
                    {
                        used_mb = usedMB,
                        total_mb = totalMB, 
                        percentage= 100.0 * usedMB / totalMB
                    };
                }
            }
            return new RamInfo();
        }
        private Dictionary<string, long> GetDisksFreeSpace()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .ToDictionary(
                    d => d.Name.TrimEnd('\\'),
                    d => d.TotalFreeSpace / 1024 / 1024 / 1024
                );
        }
        private (Dictionary<string, bool> RequiredProcesses, List<string> AllProcesses) CheckProcesses(List<string> requiredNames)
        {
            var allProcesses = Process.GetProcesses()
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (requiredNames == null)
                return (new Dictionary<string, bool>(), allProcesses);

            var runningLower = allProcesses.Select(p => p.ToLower()).ToHashSet();

            var required = requiredNames.ToDictionary(
                name => name,
                name => runningLower.Contains(name.ToLower())
            );

            return (required, allProcesses);
        }

        public class SystemCollector
        {
            private readonly List<PerformanceCounter> _coreCounters;
            private readonly PerformanceCounter _totalCpuCounter;
            private bool _isInitialized = false;

            public SystemCollector()
            {
                var coreCount = Environment.ProcessorCount;
                _coreCounters = new List<PerformanceCounter>();

                for (int i = 0; i < coreCount; i++)
                {
                    _coreCounters.Add(new PerformanceCounter("Processor", "% Processor Time", i.ToString()));
                }

                _totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }

            public async Task<List<double>> GetCpuUsagePerCore()
            {
                // TODO: использовать лок
                if (!_isInitialized)
                {
                    // Первый запуск — прогреваем счетчики
                    foreach (var counter in _coreCounters)
                        counter.NextValue();
                    _totalCpuCounter.NextValue();

                    await Task.Delay(500);
                    _isInitialized = true;
                }

                // Все последующие вызовы работают корректно
                return _coreCounters.Select(c => Math.Round(c.NextValue(), 1)).ToList();
            }

            public async Task<double> GetTotalCpuUsage()
            {
                if (!_isInitialized)
                {
                    await GetCpuUsagePerCore(); // прогреваем заодно
                }

                return Math.Round(_totalCpuCounter.NextValue(), 1);
            }
        }
        #endregion
    }
}
