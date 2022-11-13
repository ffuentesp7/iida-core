using Iida.Shared.Agromet;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class AgrometScraper : IScraper {
	private readonly string _tempFolder;
	private readonly Parameters _parameters;
	public AgrometScraper(string tempFolder, Parameters parameters) {
		_tempFolder = tempFolder;
		_parameters = parameters;
	}
	public async Task<(IEnumerable<string>, IEnumerable<string>)> Execute(Order order) {
		var dates = new List<string>();
		var paths = new List<string>();
		return (dates, paths);
	}
}