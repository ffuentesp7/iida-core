using Iida.Shared.Agromet;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class AgrometScraper : IScraper {
	private readonly string _tempFolder;
	private readonly IEnumerable<string> _dates;
	private readonly Parameters _parameters;
	public List<string> Paths { get; set; } = new List<string>();
	public AgrometScraper(string tempFolder, IEnumerable<string> dates, Parameters parameters) {
		_tempFolder = tempFolder;
		_dates = dates;
		_parameters = parameters;
	}
	public async Task Execute(Order order) {

	}
}