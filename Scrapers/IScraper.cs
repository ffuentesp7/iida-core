using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	void Execute(Order? order);
}