using Iida.Shared.Models;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;
	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public Task ExecuteStrategy(Order order, double latitude, double longitude) => _scraperStrategy!.Execute(order, latitude, longitude);
}