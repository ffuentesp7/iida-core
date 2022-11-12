﻿using System.Diagnostics;
using System.Text;

using Iida.Core.Scrapers;
using Iida.Shared.Requests;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var builder = new ConfigurationBuilder();
if (Debugger.IsAttached) {
	_ = builder.AddUserSecrets<Program>();
}
var configurationRoot = builder.Build();

string? agrometApi;
string? agrometHostname;
string? agrometLocation;
string? googleCloudCredentialFile;
string? googleCloudStorageBucket;
string? mySqlConnectionString;
string? rabbitMqHostname;
string? rabbitMqQueue;
string? usgsApi;
string? usgsLogin;
string? usgsLogout;
string? usgsSearchScene;
string? usgsDownloadScene;
string? usgsUsername;
string? usgsPassword;
if (Debugger.IsAttached) {
	agrometApi = configurationRoot.GetSection("AGROMET_API").Value;
	agrometHostname = configurationRoot.GetSection("AGROMET_HOSTNAME").Value;
	agrometLocation = configurationRoot.GetSection("AGROMET_LOCATION").Value;
	googleCloudCredentialFile = configurationRoot.GetSection("GOOGLE_CLOUD_CREDENTIAL_FILE").Value;
	googleCloudStorageBucket = configurationRoot.GetSection("GOOGLE_CLOUD_STORAGE_BUCKET").Value;
	mySqlConnectionString = configurationRoot.GetSection("MYSQL_CONNECTIONSTRING").Value;
	rabbitMqHostname = configurationRoot.GetSection("RABBITMQ_HOST").Value;
	rabbitMqQueue = configurationRoot.GetSection("RABBITMQ_QUEUE").Value;
	usgsApi = configurationRoot.GetSection("USGS_API").Value;
	usgsLogin = configurationRoot.GetSection("USGS_LOGIN").Value;
	usgsLogout = configurationRoot.GetSection("USGS_LOGOUT").Value;
	usgsSearchScene = configurationRoot.GetSection("USGS_SEARCH_SCENE").Value;
	usgsDownloadScene = configurationRoot.GetSection("USGS_DOWNLOAD_SCENE").Value;
	usgsUsername = configurationRoot.GetSection("USGS_USERNAME").Value;
	usgsPassword = configurationRoot.GetSection("USGS_PASSWORD").Value;
} else {
	agrometApi = Environment.GetEnvironmentVariable("AGROMET_API");
	agrometHostname = Environment.GetEnvironmentVariable("AGROMET_HOSTNAME");
	agrometLocation = Environment.GetEnvironmentVariable("AGROMET_LOCATION");
	googleCloudCredentialFile = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_CREDENTIAL_FILE");
	googleCloudStorageBucket = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_STORAGE_BUCKET");
	mySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
	rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
	rabbitMqQueue = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE");
	usgsApi = Environment.GetEnvironmentVariable("USGS_API");
	usgsLogin = Environment.GetEnvironmentVariable("USGS_LOGIN");
	usgsLogout = Environment.GetEnvironmentVariable("USGS_LOGOUT");
	usgsSearchScene = Environment.GetEnvironmentVariable("USGS_SEARCH_SCENE");
	usgsDownloadScene = Environment.GetEnvironmentVariable("USGS_DOWNLOAD_SCENE");
	usgsUsername = Environment.GetEnvironmentVariable("USGS_USERNAME");
	usgsPassword = Environment.GetEnvironmentVariable("USGS_PASSWORD");
}
var agrometParameters = new Iida.Shared.Agromet.Parameters {
	Api = agrometApi,
	Hostname = agrometHostname,
	Location = agrometLocation
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
	Queue = rabbitMqQueue
};
var usgsParameters = new Iida.Shared.Usgs.Parameters {
	Api = usgsApi,
	Login = usgsLogin,
	Logout = usgsLogout,
	SearchScene = usgsSearchScene,
	DownloadScene = usgsDownloadScene,
	Username = usgsUsername,
	Password = usgsPassword
};

var scaperContext = new ScraperContext();
var factory = new ConnectionFactory() { HostName = rabbitMqHostname };
using (var connection = factory.CreateConnection()) {
	using var channel = connection.CreateModel();
	_ = channel.QueueDeclare(queue: rabbitMqQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
	channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
	Console.WriteLine("Waiting for order requests...");
	var consumer = new EventingBasicConsumer(channel);

	consumer.Received += (sender, ea) => {
		try {
			var body = ea.Body.ToArray();
			var message = Encoding.UTF8.GetString(body);
			Console.WriteLine($"Received order: {message}");
			var order = JsonConvert.DeserializeObject<Order>(message);
			channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
		} catch (JsonReaderException) {
			Console.WriteLine("GeoJSON format error");
		}
	};

	_ = channel.BasicConsume(queue: rabbitMqQueue, autoAck: false, consumer: consumer);
	Console.WriteLine("Press the ENTER key to exit");
	_ = Console.ReadLine();
}