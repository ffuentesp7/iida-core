using Iida.Shared.DataTransferObjects;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task Execute(Request request, double latitude, double longitude);
}