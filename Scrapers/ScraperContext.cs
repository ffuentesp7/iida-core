﻿using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class ScraperContext {
	private IScraper? _scraperStrategy;

	public void SetStrategy(IScraper scraperStrategy) => _scraperStrategy = scraperStrategy;
	public void ExecuteStrategy(Order order) => _scraperStrategy!.Execute(order);
}