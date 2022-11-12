using System.Diagnostics;
using System.Text;

using Iida.Core.Scrapers;
using Iida.Shared.Requests;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

string? mySqlConnectionString;
string? rabbitMqHost;
string? rabbitMqQueue;

var builder = new ConfigurationBuilder();
if (Debugger.IsAttached) {
	_ = builder.AddUserSecrets<Program>();
}
var configurationRoot = builder.Build();

if (Debugger.IsAttached) {
	mySqlConnectionString = configurationRoot.GetSection("MYSQL_CONNECTIONSTRING").Value;
	rabbitMqHost = configurationRoot.GetSection("RABBITMQ_HOST").Value;
	rabbitMqQueue = configurationRoot.GetSection("RABBITMQ_QUEUE").Value;
} else {
	Console.WriteLine("Prod");
	mySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
	rabbitMqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
	rabbitMqQueue = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE");
}

var scaperContext = new ScraperContext();
var factory = new ConnectionFactory() { HostName = rabbitMqHost };
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