using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task Execute(Order order, double latitude, double longitude);
}