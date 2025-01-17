using System.Diagnostics;
using System.Globalization;
using backupdataBase.Hubs;
using backupdataBase.Models;
using backupdataBase.Models.SiteDataBaseContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;

namespace backupdataBase.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IHubContext<BackupHub> _hubContext;
        private readonly IWebHostEnvironment _env;
        const string format = "yyyyMMddHHmmss";
        //yyyyMMddHHmmss
        private readonly SiteContext _context;
        private readonly DatabaseService _databaseService;
        private readonly IConfiguration _configuration;
        string connectionString = string.Empty;
        // Path to save the backup file 
        string backupDirectory = "";
        string backupFileName = string.Empty;
        string backupFilePath = string.Empty;

        string dataBaseName = "SiteContext";
        public HomeController(ILogger<HomeController> logger,
            IHubContext<BackupHub> hubContext, IWebHostEnvironment env,
            IConfiguration configuration, DatabaseService databaseService, SiteContext context)
        {
            _logger = logger;

            _hubContext = hubContext;
            _env = env;
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection");
            _databaseService = databaseService;
            _context = context;
            //  _context = context;
        }

        public IActionResult Index()
        {

            Console.WriteLine($"{_env.WebRootPath}/BackUp/");
            ViewBag.ListBackupFile = ListOfBackUP();
            return View();
        }






        public async Task<IActionResult> BackUp()
        {
            backupFileName = $"{DateTime.Now.ToString(format)}_Backup.bak";
            backupDirectory = $"{_env.WebRootPath}/BackUp/";
            backupFilePath = $"{backupDirectory}/{backupFileName}";
            Console.WriteLine();
            if (!Directory.Exists(backupDirectory))
            { Directory.CreateDirectory(backupDirectory); }
            var progress = new Progress<int>(async percent =>
            {
                Console.CursorLeft = 0;
                Console.Write($"Progress: {percent}%  ");
                await _hubContext.Clients.All.SendAsync("GetBackUpProcess", percent);
                Console.Write(backupFilePath);
            });
            await BackupDatabaseAsync(connectionString, backupFilePath, progress);
            Console.WriteLine();
            Console.WriteLine("Database backup completed successfully.");
            return Content("Database backup completed successfully.");

        }


        public async Task BackupDatabaseAsync(string connectionString, string backupFilePath, IProgress<int> progress)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string backupQuery = $"BACKUP DATABASE {dataBaseName} TO DISK='{backupFilePath}' WITH FORMAT, MEDIANAME='DbBackups', NAME='Full Backup of {dataBaseName}'";
                using (SqlCommand command = new SqlCommand(backupQuery, connection))
                {
                    command.CommandTimeout = 0;
                    await command.ExecuteNonQueryAsync();
                }
                // Simulate progress for demonstration 
                for (int i = 0; i <= 100; i++)
                {
                    await Task.Delay(50); // Simulate work being done 
                    progress.Report(i);
                }
            }
        }
        [HttpPost]
        public async Task<IActionResult> Restore(string backupName)
        {

            backupDirectory = $"{_env.WebRootPath}/BackUp";
            backupFilePath = $"{backupDirectory}/{backupName}";

            await RestoreDatabaseAsync(connectionString, backupFilePath);

            return View();
        }
        private async Task RestoreDatabaseAsync(string connectionString, string backupFilePath)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection; // Subscribe to InfoMessage event to get restore progress
                    connection.InfoMessage += async (sender, e) =>
                    {
                        foreach (SqlError error in e.Errors)
                        {
                            if (error.Message.Contains("percent"))
                            {
                                string message = error.Message.Replace("percent processed.", "").Trim();
                                if (float.TryParse(message, out float percentage))
                                {
                                    await _hubContext.Clients.All.SendAsync("GetBackUpProcess", percentage);
                                    Console.WriteLine($"Progress: {percentage}%");
                                }
                            }
                        }
                    }; // Set database to single-user mode
                    command.CommandText = $"use master;";
                    await command.ExecuteNonQueryAsync(); await _hubContext.Clients.All.SendAsync("GetBackUpProcess", 3.0f);
                    if (!await CheckDatabaseExistsAsync(connectionString, dataBaseName))
                    {
                       await UseMaster(connectionString, $"create database {dataBaseName}");
                    }
                    command.CommandText = $"ALTER DATABASE {dataBaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                    await command.ExecuteNonQueryAsync(); await _hubContext.Clients.All.SendAsync("GetBackUpProcess", 30.3f);
                    // 33.3% progress // Restore the database
                    command.CommandText = $"RESTORE DATABASE {dataBaseName} FROM DISK = @backupFilePath WITH REPLACE";
                    command.Parameters.AddWithValue("@backupFilePath", backupFilePath); await command.ExecuteNonQueryAsync();
                    await _hubContext.Clients.All.SendAsync("GetBackUpProcess", 66.6f);
                    // 66.6% progress // Set database to multi-user mode
                    command.CommandText = $"ALTER DATABASE {dataBaseName} SET MULTI_USER";
                    await command.ExecuteNonQueryAsync();
                    await _hubContext.Clients.All.SendAsync("GetBackUpProcess", 100f); // 100% progress
                    await command.ExecuteNonQueryAsync();
                    Console.WriteLine("Database restored successfully.");
                }

            }
        }
        public static async Task<bool> CheckDatabaseExistsAsync(string connectionString, string databaseName )
        {
            string query = $"IF DB_ID('{databaseName}') IS NOT NULL SELECT 1 ELSE SELECT 0";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(); using (SqlCommand command = new SqlCommand(query, connection))
                { int result = (int)await command.ExecuteScalarAsync(); return result == 1; }
            }
        }
        private async Task UseMaster(string connectionString, string query)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = new SqlCommand
                {
                    Connection = connection,
                    CommandText = query
                };


                await command.ExecuteNonQueryAsync();

            }

        }


        private List<KeyValuePair<string, string>> ListOfBackUP() =>

         System.IO.Directory.GetFiles($"{_env.WebRootPath}/BackUp/", "*.bak").
                  ToList()
                 .Select(x => new KeyValuePair<string, string>($"{stringToDate(GetFileName(x)).ToString()} - " +
                     $"{Math.Round((GetFileInfo(x).Length / (1024.0 * 1024.0)), 2)} MB"
                     ,
                     $"{GetFileName(x)}"))
             .OrderByDescending(x => x.Key)
             .ToList();


        private string GetFileName(string fileName)
            => $"{System.IO.Path.GetFileName(fileName)} ";
        private DateTime stringToDate(string fileName)
          => DateTime.ParseExact(new string(fileName.Where(char.IsDigit).ToArray()),
               format, CultureInfo.InvariantCulture);
        public async Task<IActionResult> Create()
        {
            var text = await System.IO.File.ReadAllLinesAsync($"{_env.WebRootPath}/titles.txt");
            foreach (var item in text)
            {
                Products p = new Products()
                {
                    DateOfCreated = DateTime.Now,
                    Title = item.ToString()
                };

                await _context.Products.AddAsync(p);
            }

            await _context.SaveChangesAsync();
            return Json(
                await _context.Products.ToListAsync()
                );
        }

        private FileInfo GetFileInfo(string path)
        {
            var info = new FileInfo(path);
            Console.WriteLine($" --------------sie :{info.Length / (1024.0 * 1024.0)} Mb ------------- ");
            return info;
        }

    }
}