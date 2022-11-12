using System.Net;

using Iida.Shared;
using Iida.Shared.Requests;

namespace Iida.Core.Scrapers;

internal class UsgsScraper : IScraper {
	public async Task Execute(Order? order, Configuration?[]? configurations) {
		try {
			var googleCloudParameters = (Shared.GoogleCloud.Parameters?)configurations![0];
			var usgsParameters = (Shared.Usgs.Parameters?)configurations[1];
			var apiClient = new HttpClient {
				Timeout = TimeSpan.FromMinutes(10)
			};
			var cookies = new CookieContainer();
			var websiteClient = new HttpClient(new HttpClientHandler {
				AllowAutoRedirect = false,
				CookieContainer = cookies
			});
			var downloadClient = new HttpClient(new HttpClientHandler { CookieContainer = cookies }) {
				Timeout = TimeSpan.FromMinutes(10)
			};
			var website = await websiteClient.GetAsync($"{usgsParameters!.Login}");
			if (website.IsSuccessStatusCode) {
			}
		} catch (HttpRequestException) {
			Console.WriteLine("Sin conexión");
		} catch (TaskCanceledException) {
			Console.WriteLine("Ejecución interrumpida");
		} catch (NullReferenceException) {
			Console.WriteLine("Error de programa: referencia nula");
		}
	}
}