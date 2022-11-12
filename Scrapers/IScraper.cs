using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task Execute(Order? order, Configuration?[]? configurations);
}