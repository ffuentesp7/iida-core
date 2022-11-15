using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	List<String> Paths { get; set; }
	Task Execute(QueueRequest queueRequest, double latitude, double longitude);
}