using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class AgrometScraper : IScraper {
	public async Task<(IEnumerable<string>, IEnumerable<string>)> Execute(Order order, string tempFolder, params Configuration[] configurations) {
		var dates = new List<string>();
		var paths = new List<string>();
		return (dates, paths);
	}
}