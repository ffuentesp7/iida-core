using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;
	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public Task ExecuteStrategy(Order order, string tempFolder, params Configuration[] configurations) => _scraperStrategy!.Execute(order, tempFolder, configurations);
}