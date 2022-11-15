using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;
	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public Task ExecuteStrategy(Request request, double latitude, double longitude) => _scraperStrategy!.Execute(request, latitude, longitude);
}