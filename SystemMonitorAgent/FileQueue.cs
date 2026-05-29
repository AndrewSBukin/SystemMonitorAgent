using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemMonitorAgent
{
    public class FileQueue
    {
        private readonly string _queueFolder = @"C:\QueueData";
        private readonly int _maxQueueSize = 10000;
        private readonly int _maxAttempts = 10;
        private Logger _logger;

        public FileQueue(AppSettings settings, Logger logger)
        {
            _logger = logger;
            try
            {
                _queueFolder = settings.QueueFolder;
                Directory.CreateDirectory(_queueFolder);
            }
            catch (Exception)
            {
                _logger.Log("Каталог для хранения очереди недоступен", true);
            }
        }

        public async Task EnqueueAsync(string data)
        {
            try
            {
                var files = Directory.GetFiles(_queueFolder, "*.json").ToList();

                while (files.Count >= _maxQueueSize)
                {
                    var oldest = files.OrderBy(f => File.GetCreationTime(f)).First();
                    File.Delete(oldest);
                    files.Remove(oldest);
                }

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_0.json";
                await File.WriteAllTextAsync(Path.Combine(_queueFolder, fileName), data);
            }
            catch (Exception)
            {
                _logger.Log("Не удалось записать данные в очередь", true);
            }
        }

        public async Task<string?> DequeueAsync()
        {
            var file = Directory.GetFiles(_queueFolder, "*.json")
                .OrderBy(f => File.GetCreationTime(f))
                .FirstOrDefault();

            if (file == null) return null;

            try
            {
                var content = await File.ReadAllTextAsync(file);

                var attempts = int.Parse(Path.GetFileNameWithoutExtension(file).Split('_').Last());

                if (attempts >= _maxAttempts)
                {
                    File.Delete(file);
                    return null;
                }
                else
                {
                    var filename = Path.GetFileNameWithoutExtension(file);
                    var idx = filename.LastIndexOf('_');
                    File.Move(file, Path.Combine(_queueFolder, filename.Substring(0, idx) + "_" + (attempts + 1) + ".json"));
                }

                return content;
            }
            catch (Exception)
            {
                File.Delete(file); // неправильный файл
                return await DequeueAsync();
            }
        }

        internal async Task DequeueDeleteAsync()
        {
            var file = Directory.GetFiles(_queueFolder, "*.json")
                .OrderBy(f => File.GetCreationTime(f))
                .FirstOrDefault();

            if (file != null)
            {
                File.Delete(file);
            }
        }
    }
}
