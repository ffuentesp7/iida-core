using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task<(IEnumerable<string>, IEnumerable<string>)> Execute(Order order);
}