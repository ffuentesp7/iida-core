using System.Diagnostics;
using System.Text;

using GeoJSON.Net.Geometry;

using Iida.Core;
using Iida.Core.Contexts;
using Iida.Core.Scrapers;
using Iida.Shared.DataTransferObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = new ConfigurationBuilder();
if (Debugger.IsAttached) {
	_ = builder.AddUserSecrets<Program>();
}
var configurationRoot = builder.Build();
string? ranApi;
string? ranHostname;
string? ranLocation;
string? ranTimeout;
string? googleCloudCredentialFile;
string? googleCloudStorageBucket;
string? mySqlConnectionString;
string? rabbitMqHostname;
string? rabbitMqPassword;
string? rabbitMqQueue;
string? rabbitMqUsername;
string? usgsApi;
string? usgsDataset;
string? usgsLogin;
string? usgsLogout;
string? usgsSearchScene;
string? usgsDownloadScene;
string? usgsUsername;
string? usgsPassword;
string? usgsTimeout;
if (Debugger.IsAttached) {
	ranApi = configurationRoot.GetSection("RAN_API").Value;
	ranHostname = configurationRoot.GetSection("RAN_HOSTNAME").Value;
	ranLocation = configurationRoot.GetSection("RAN_LOCATION").Value;
	ranTimeout = configurationRoot.GetSection("RAN_TIMEOUT").Value;
	googleCloudCredentialFile = configurationRoot.GetSection("GOOGLE_CLOUD_CREDENTIAL_FILE").Value;
	googleCloudStorageBucket = configurationRoot.GetSection("GOOGLE_CLOUD_STORAGE_BUCKET").Value;
	mySqlConnectionString = configurationRoot.GetSection("MYSQL_CONNECTIONSTRING").Value;
	rabbitMqHostname = configurationRoot.GetSection("RABBITMQ_HOST").Value;
	rabbitMqPassword = configurationRoot.GetSection("RABBITMQ_PASSWORD").Value;
	rabbitMqQueue = configurationRoot.GetSection("RABBITMQ_QUEUE").Value;
	rabbitMqUsername = configurationRoot.GetSection("RABBITMQ_USERNAME").Value;
	usgsApi = configurationRoot.GetSection("USGS_API").Value;
	usgsDataset = configurationRoot.GetSection("USGS_DATASET").Value;
	usgsLogin = configurationRoot.GetSection("USGS_LOGIN").Value;
	usgsLogout = configurationRoot.GetSection("USGS_LOGOUT").Value;
	usgsSearchScene = configurationRoot.GetSection("USGS_SEARCH_SCENE").Value;
	usgsDownloadScene = configurationRoot.GetSection("USGS_DOWNLOAD_SCENE").Value;
	usgsUsername = configurationRoot.GetSection("USGS_USERNAME").Value;
	usgsPassword = configurationRoot.GetSection("USGS_PASSWORD").Value;
	usgsTimeout = configurationRoot.GetSection("USGS_TIMEOUT").Value;
} else {
	ranApi = Environment.GetEnvironmentVariable("RAN_API");
	ranHostname = Environment.GetEnvironmentVariable("RAN_HOSTNAME");
	ranLocation = Environment.GetEnvironmentVariable("RAN_LOCATION");
	ranTimeout = Environment.GetEnvironmentVariable("RAN_TIMEOUT");
	googleCloudCredentialFile = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_CREDENTIAL_FILE");
	googleCloudStorageBucket = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_STORAGE_BUCKET");
	mySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
	rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
	rabbitMqPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");
	rabbitMqQueue = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE");
	rabbitMqUsername = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME");
	usgsApi = Environment.GetEnvironmentVariable("USGS_API");
	usgsDataset = Environment.GetEnvironmentVariable("USGS_DATASET");
	usgsLogin = Environment.GetEnvironmentVariable("USGS_LOGIN");
	usgsLogout = Environment.GetEnvironmentVariable("USGS_LOGOUT");
	usgsSearchScene = Environment.GetEnvironmentVariable("USGS_SEARCH_SCENE");
	usgsDownloadScene = Environment.GetEnvironmentVariable("USGS_DOWNLOAD_SCENE");
	usgsUsername = Environment.GetEnvironmentVariable("USGS_USERNAME");
	usgsPassword = Environment.GetEnvironmentVariable("USGS_PASSWORD");
	usgsTimeout = Environment.GetEnvironmentVariable("USGS_TIMEOUT");
}
var ranParameters = new Iida.Shared.RanForIidaR.Parameters {
	Api = ranApi,
	Hostname = ranHostname,
	Location = ranLocation,
	Timeout = ranTimeout,
};
var googleCloudParameters = new Iida.Shared.GoogleCloud.Parameters {
	CredentialFile = googleCloudCredentialFile,
	StorageBucket = googleCloudStorageBucket
};
var mySqlParameters = new Iida.Shared.MySql.Parameters {
	ConnectionString = mySqlConnectionString
};
var rabbitMqParameters = new Iida.Shared.RabbitMq.Parameters {
	Hostname = rabbitMqHostname,
	Password = rabbitMqPassword,
	Queue = rabbitMqQueue,
	Username = rabbitMqUsername
};
var usgsParameters = new Iida.Shared.Usgs.Parameters {
	Api = usgsApi,
	Dataset = usgsDataset,
	Login = usgsLogin,
	Logout = usgsLogout,
	SearchScene = usgsSearchScene,
	DownloadScene = usgsDownloadScene,
	Username = usgsUsername,
	Password = usgsPassword,
	Timeout = usgsTimeout
};
var scraperContext = new ScraperContext();
var factory = new ConnectionFactory() { HostName = rabbitMqHostname, UserName = rabbitMqUsername, Password = rabbitMqPassword };
using (var connection = factory.CreateConnection()) {
	using var channel = connection.CreateModel();
	_ = channel.QueueDeclare(queue: rabbitMqQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
	channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
	Console.WriteLine("Waiting for requests...");
	var consumer = new EventingBasicConsumer(channel);
	consumer.Received += async (sender, ea) => {
		try {
			Console.WriteLine($"Creating folder...");
			var userFolder = $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "iida")}";
			_ = Directory.CreateDirectory(userFolder);
			var body = ea.Body.ToArray();
			var message = Encoding.UTF8.GetString(body);
			Console.WriteLine($"Order received. Processing now...");
			var queueRequest = JsonConvert.DeserializeObject<QueueRequest>(message);
			using var context = new AppDbContext(mySqlConnectionString!);
			var order = await context.Orders!.FirstOrDefaultAsync(o => o.Guid == queueRequest!.Guid);
			order!.Status = "Processing";
			_ = await context.SaveChangesAsync();
			Console.WriteLine("Calculating centroid of polygon...");
			var polygon = (Polygon)queueRequest!.GeoJson!.Features[0].Geometry;
			var lineString = polygon.Coordinates[0];
			var vertexes = lineString.Coordinates;
			var (latitude, longitude) = Centroid.Calculate(vertexes);
			Console.WriteLine($"Centroid calculated: ({latitude}; {longitude})");
			var usgsScraper = new UsgsScraper(userFolder, usgsParameters);
			scraperContext.SetStrategy(usgsScraper);
			await scraperContext.ExecuteStrategy(queueRequest!, latitude, longitude);
			var ranScraperForIidaR = new RanScraperForIidaR(userFolder, usgsScraper.Dates, usgsScraper.EntityIds, ranParameters);
			scraperContext.SetStrategy(ranScraperForIidaR);
			await scraperContext.ExecuteStrategy(queueRequest!, latitude, longitude);
			channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
			order!.Status = "Completed";
			_ = await context.SaveChangesAsync();
			Console.WriteLine("Order completed");
		} catch (JsonReaderException) {
			Console.WriteLine("GeoJSON format error");
		} catch (NullReferenceException) {
			Console.WriteLine("Null reference detected");
		} catch {
			Console.WriteLine("Error");
		}
	};
	_ = channel.BasicConsume(queue: rabbitMqQueue, autoAck: false, consumer: consumer);
	Console.WriteLine("Press the ENTER key to exit");
	_ = Console.ReadLine();
}