﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DyflexisExport.Models;
using DyflexisExport.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;

namespace DyflexisExport.Services
{
	public class DyflexisHtmlScraper : IDisposable, IDyflexisHtmlScraper
	{
		private const string LoginUrl = "login/authenticate";

		private static readonly TimeSpan LoginTimeout = new TimeSpan(12, 0, 0);

		private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
		private readonly CookieContainer _cookieContainer = new CookieContainer();
		private readonly HttpClient _httpClient;

		private string _appUrl;

		private DateTime? _lastLogin;

		public DyflexisHtmlScraper()
		{
			var httpClientHandler = new HttpClientHandler
			{
				CookieContainer = _cookieContainer,
				AllowAutoRedirect = false
			};

			_httpClient = new HttpClient(httpClientHandler);
		}

		private async Task<bool> Login()
		{
			if (_lastLogin.HasValue && DateTime.Now.Subtract(_lastLogin.Value) <= LoginTimeout)
				return true;

			_logger.Info("Logging in");

			var loginModel = new Login
			{
				Username = SettingsService.Settings.Username,
				Password = SettingsService.Settings.Password,
				RememberMe = true
			};

			var json = JsonConvert.SerializeObject(loginModel, new JsonSerializerSettings
			{
				ContractResolver = new CamelCasePropertyNamesContractResolver()
			});
			var formData = new StringContent(json);
			formData.Headers.ContentType = new MediaTypeHeaderValue("application/json");
			formData.Headers.ContentLength = json.Length;
			
			try
			{
				using var postRequest = new HttpRequestMessage(HttpMethod.Post, SettingsService.Settings.Url + LoginUrl) { Content = formData };
				using var response = await _httpClient.SendAsync(postRequest);

				if (!response.IsSuccessStatusCode)
				{
					_logger.Error($"Failed to log in for scraping. Status code: {response.StatusCode}");
					return false;
				}

				var responseJson = await response.Content.ReadAsStringAsync();
				var parsedResponse = JsonConvert.DeserializeObject<LoginResponse>(responseJson);

				_appUrl = parsedResponse.Url;
			}
			catch (Exception e)
			{
				_logger.Error(e, "Failed to login for scraping.");
				return false;
			}

			_lastLogin = DateTime.Now;

			_logger.Info("Successfully logged into Dyflexis.");

			return true;
		}

		private static string GetRandomSeed() =>
			((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString();

		public async Task<string> GetMonthHtml(int year, int month)
		{
			if (!await Login())
				return null;

			var scheduleUrl = $"{_appUrl}rooster2/index2?periode={year}-{month}&_={GetRandomSeed()}";

			_logger.Info($"Scraping URL {scheduleUrl}");

			try
			{

				using (var request = new HttpRequestMessage(HttpMethod.Get, scheduleUrl))
				{
					request.Headers.Add("X-Requested-With", "XMLHttpRequest");

					using (var response = await _httpClient.SendAsync(request))
					{
						if (!response.IsSuccessStatusCode)
						{
							_logger.Error($"Failed to get schedule html, response code: {response.StatusCode}");
							return null;
						}

						var responseHtml = await response.Content.ReadAsStringAsync();

						return responseHtml;
					}
				}
			}
			catch (Exception e)
			{
				_logger.Error(e, "Failed to get schedule html.");
			}

			return null;
		}

		public void Dispose()
		{
			_httpClient?.Dispose();
		}
	}
}
