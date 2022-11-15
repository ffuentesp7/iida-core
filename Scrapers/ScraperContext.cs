using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;
	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public Task ExecuteStrategy(QueueRequest queueRequest, double latitude, double longitude) => _scraperStrategy!.Execute(queueRequest, latitude, longitude);
	public List<T> CreateResults<T>(Shared.Models.Order order) where T : Shared.Models.Result, new() {
		var results = new List<T>();
		var urls = _scraperStrategy!.Urls;
		foreach (var url in urls) {
			var result = new T {
				Guid = Guid.NewGuid(),
				Timestamp = DateTimeOffset.Now,
				Url = url,
				Order = order
			};
			results.Add(result);
		}
		return results;
	}
}