using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task Execute(QueueRequest queueRequest, double latitude, double longitude);
}