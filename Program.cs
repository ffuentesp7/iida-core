using System.Text;

using Iida.Core.Scrapers;
using Iida.Shared.Requests;

using Newtonsoft.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var scaperContext = new ScraperContext();
var factory = new ConnectionFactory() { HostName = "localhost" };
using (var connection = factory.CreateConnection()) {
	using var channel = connection.CreateModel();
	Console.WriteLine("Waiting for order requests...");
	var consumer = new EventingBasicConsumer(channel);

	consumer.Received += (sender, ea) => {
		var body = ea.Body.ToArray();
		var message = Encoding.UTF8.GetString(body);
		Console.WriteLine($"Received order: {message}");
		var order = JsonConvert.DeserializeObject<Order>(message);
		Console.WriteLine(order!.GeoJsonString);
		channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
	};

	_ = channel.BasicConsume(queue: "iida-queue", autoAck: false, consumer: consumer);
	Console.WriteLine("Press the ENTER key to exit");
	_ = Console.ReadLine();
}