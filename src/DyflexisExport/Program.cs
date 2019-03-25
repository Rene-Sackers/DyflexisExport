using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DyflexisExport.Models;
using DyflexisExport.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using HtmlAgilityPack;
using Newtonsoft.Json;

//using OAuth2.Client.Impl;
//using OAuth2.Configuration;
//using OAuth2.Infrastructure;

namespace DyflexisExport
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			await new ProgramInstance().Run();
			Console.ReadKey();
		}
	}

	public class ProgramInstance
	{
		private static readonly Regex AssignmentIdRegex = new Regex("assignment://(?<id>\\d+)", RegexOptions.Compiled);

		private readonly CookieContainer _cookieContainer = new CookieContainer();
		private readonly HttpClient _httpClient;
		//private readonly GoogleClient _oauthClient;

		private const int ScrapeMonthsCount = 2;

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

			//var clientConfiguration = new RuntimeClientConfiguration
			//{
			//	ClientId = SettingsService.Settings.GoogleClientId,
			//	ClientSecret = SettingsService.Settings.GoogleClientSecret,
			//	Scope = "https://www.googleapis.com/auth/calendar.events",
			//	RedirectUri = "urn:ietf:wg:oauth:2.0:oob"
			//};

			//var requestFactory = new RequestFactory();

			//_oauthClient = new GoogleClient(requestFactory, clientConfiguration);
		}

		public async Task Run()
		{
			await EnsureGoogleAuthentication();
			
			await EnsureCalendarId();

			return;

			if (false && !await Login())
			{
				Console.WriteLine("Failed to log in.");
				return;
			}

			for (var month = 0; month < ScrapeMonthsCount; month++)
			{
				var targetMonth = DateTime.Now.AddMonths(month);
				Console.WriteLine($"Scraping month {month + 1}/{ScrapeMonthsCount} ({targetMonth.Year}-{targetMonth.Month})");

				//var monthHtml = await ScrapeMonth(DateTime.Now.AddMonths(month));
				var monthHtml = File.ReadAllText($"month html\\{targetMonth.Year}-{targetMonth.Month}.html");
				var assignments = ParseMonthHtml(monthHtml).ToArray();
			}
		}

		private async Task<UserCredential> GetUserCredentials()
		{
			var clientSecrets = new ClientSecrets
			{
				ClientId = SettingsService.Settings.GoogleClientId,
				ClientSecret = SettingsService.Settings.GoogleClientSecret
			};

			if (SettingsService.Settings.GoogleApiTokenResponse != null)
			{
				var initializer = new GoogleAuthorizationCodeFlow.Initializer()
				{
					ClientSecrets = clientSecrets
				};

				return new UserCredential(new GoogleAuthorizationCodeFlow(initializer), "user", SettingsService.Settings.GoogleApiTokenResponse);
			}
			
			return await GoogleWebAuthorizationBroker.AuthorizeAsync(
				clientSecrets,
				new[] { "https://www.googleapis.com/auth/calendar" },
				"user",
				CancellationToken.None,
				new DataStoreImplementation()
			);
		}

		private async Task EnsureCalendarId()
		{
			if (!string.IsNullOrEmpty(SettingsService.Settings.TargetCalendarId))
				return;
			
			int calendarId;
			while (true)
			{
				Console.WriteLine("Choose a calendar.");

				if (int.TryParse(Console.ReadLine(), out calendarId))
					break;

				Console.Write("Calendar nr.: ");
			}
		}

		private async Task EnsureGoogleAuthentication()
		{
			var userCredentials = await GetUserCredentials();

			var service = new CalendarService(new BaseClientService.Initializer
			{
				HttpClientInitializer = userCredentials,
				ApplicationName = "DyflexisExport"
			});

			var listCalendarsRequest = service.CalendarList.List();
			var calendarList = await listCalendarsRequest.ExecuteAsync();
			foreach (var calendar in calendarList.Items)
			{
				Console.WriteLine($"[{calendar.Id}] {calendar.Summary}");
			}

			// Define parameters of request.
			var request = service.Events.List(SettingsService.Settings.TargetCalendarId);
			request.TimeMin = DateTime.Now;
			request.ShowDeleted = false;
			request.SingleEvents = true;
			request.MaxResults = 10;
			request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

			// List events.
			var events = request.Execute();
			Console.WriteLine("Upcoming events:");
			if (events.Items != null && events.Items.Count > 0)
			{
				foreach (var eventItem in events.Items)
				{
					var when = eventItem.Start.DateTime.ToString();
					if (string.IsNullOrEmpty(when))
					{
						when = eventItem.Start.Date;
					}
					Console.WriteLine("{0} ({1})", eventItem.Summary, when);
				}
			}
			else
			{
				Console.WriteLine("No upcoming events found.");
			}
			Console.Read();

			//if (string.IsNullOrWhiteSpace(SettingsService.Settings.AuthorizationToken))
			//	GetNewAuthToken();

			//var userInfo = _oauthClient.GetUserInfo(new NameValueCollection
			//{
			//	{"refresh_token", SettingsService.Settings.AuthorizationToken},
			//	{"grant_type", "refresh_token"}
			//});
		}

		private async Task<bool> Login()
		{
			Console.WriteLine("Logging in");

			var formData = new MultipartFormDataContent
			{
				{new StringContent(SettingsService.Settings.Username), "xvalues[user]"},
				{new StringContent(SettingsService.Settings.Password), "xvalues[pass]"},
				{new StringContent("Log in"), "submit"}
			};

			using (var postRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = formData })
			using (var response = await _httpClient.SendAsync(postRequest))
			{
				Console.WriteLine($"Status code: {response.StatusCode}");

				return response.IsSuccessStatusCode;
			}
		}

		private static string GetRandomSeed() =>
			((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds).ToString();

		private async Task<string> ScrapeMonth(DateTime date)
		{
			Console.WriteLine($"Scraping month (YYYY/MM): {date.Year}/{date.Month}");

			var scheduleUrl = $"rooster2/index2?periode={date.Year}-{date.Month}&_={GetRandomSeed()}";

			using (var request = new HttpRequestMessage(HttpMethod.Get, scheduleUrl))
			{
				request.Headers.Add("X-Requested-With", "XMLHttpRequest");

				using (var response = await _httpClient.SendAsync(request))
				{
					if (!response.IsSuccessStatusCode)
					{
						Console.WriteLine($"Failed to get schedule html, response code: {response.StatusCode}");
						return null;
					}

					var responseHtml = await response.Content.ReadAsStringAsync();

					Directory.CreateDirectory("month html");
					File.WriteAllText($"month html/{date.Year}-{date.Month}.html", responseHtml);

					Console.WriteLine("Month scraped");

					return responseHtml;
				}
			}
		}

		private static IEnumerable<WorkAssignment> ParseMonthHtml(string html)
		{
			var htmlDocument = new HtmlDocument();
			htmlDocument.LoadHtml(html);

			var days = htmlDocument.DocumentNode.SelectNodes("//table[@class='calender']//td[@title and (not(@class) or @class!='outsideMonth')]");
			foreach (var day in days)
			{
				var assignments = day.SelectNodes(".//div[contains(@uo,'assignment://')]");
				if (assignments == null || assignments.Count == 0)
					continue;

				var date = day.GetAttributeValue("title", null);
				Console.WriteLine($"=== {date} ===");

				foreach (var assignment in assignments)
				{
					var assignmentId = AssignmentIdRegex.Match(assignment.GetAttributeValue("uo", string.Empty)).Groups["id"]?.Value;
					var placement = HtmlEntity.DeEntitize(assignment.SelectSingleNode("div[@title]").InnerText);
					var timeString = assignment.SelectSingleNode("b").InnerText;
					var timeSplit = timeString.Split(" - ");
					var startTime = timeSplit[0];
					var endTime = timeSplit[1];

					var parsedStartTime = DateTime.Parse($"{date} {startTime}");
					var parsedEndTime = DateTime.Parse($"{date} {endTime}");

					Console.WriteLine($"ID: {assignmentId}, Placement: {placement}, Start: {parsedStartTime:t}, End: {parsedEndTime:t}");

					yield return new WorkAssignment
					{
						Id = assignmentId,
						Start = parsedStartTime,
						End = parsedEndTime,
						Placement = placement
					};
				}

				Console.WriteLine();
			}
		}
	}

	public class DataStoreImplementation : IDataStore
	{
		private readonly Dictionary<string, dynamic> _data = new Dictionary<string, dynamic>();

		public Task StoreAsync<T>(string key, T value)
		{
			Console.WriteLine($"Store: [{key}][{value?.GetType().FullName}]: {JsonConvert.SerializeObject(value)}");

			if (key == "user")
			{
				SettingsService.Settings.GoogleApiTokenResponse = value as TokenResponse;
				SettingsService.Save();

				Console.WriteLine("Stored user token/refresh token");
			}

			_data[key] = value;

			return Task.CompletedTask;
		}

		public Task DeleteAsync<T>(string key)
		{
			if (_data.ContainsKey(key))
				_data.Remove(key);

			return Task.CompletedTask;
		}

		public Task<T> GetAsync<T>(string key)
		{
			object value = null;

			if (key == "user")
				value = SettingsService.Settings.GoogleApiTokenResponse;
			else if (_data.ContainsKey(key))
				value = (T)_data[key];

			Console.WriteLine($"Get: [{key}][{value?.GetType().FullName}]: {JsonConvert.SerializeObject(value)}");

			return Task.FromResult(default(T));
		}

		public Task ClearAsync()
		{
			_data.Clear();

			return Task.CompletedTask;
		}
	}
}