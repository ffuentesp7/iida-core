﻿using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal interface IScraper {
	Task<(IEnumerable<string>, IEnumerable<string>)> Execute(Order order, string tempFolder, params Configuration[] configurations);
}