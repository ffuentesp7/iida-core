using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;
	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public Task ExecuteStrategy(QueueRequest queueRequest, double latitude, double longitude) => _scraperStrategy!.Execute(queueRequest, latitude, longitude);
}