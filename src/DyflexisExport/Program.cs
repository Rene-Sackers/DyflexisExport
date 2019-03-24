using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DyflexisExport.Services;

namespace DyflexisExport
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			await new ProgramInstance().Run();
		}
	}

	public class ProgramInstance
	{
		private readonly CookieContainer _cookieContainer = new CookieContainer();
		private readonly HttpClient _httpClient;

		public ProgramInstance()
		{
			var httpClientHandler = new HttpClientHandler
			{
				CookieContainer = _cookieContainer,
				AllowAutoRedirect = false
			};

			_httpClient = new HttpClient(httpClientHandler);
			_httpClient = new HttpClient
			{
				BaseAddress = new Uri(SettingsService.Settings.Url)
			};
		}

		public async Task Run()
		{
			await Login();
		}

		private async Task Login()
		{
			var formData = new MultipartFormDataContent
			{
				{new StringContent(SettingsService.Settings.Username), "xvalues[user]"},
				{new StringContent(SettingsService.Settings.Password), "xvalues[pass]"},
				{new StringContent("Log in"), "submit"}
			};

			var postRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty)
			{
				Content = formData
			};

			var response = await _httpClient.SendAsync(postRequest);
			if (response.IsSuccessStatusCode)
			{
				Console.WriteLine($"Failed to log in: {response.StatusCode}");
				return;
			}
		}
	}
}