using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DyflexisExport.Models;
using DyflexisExport.Services;
using HtmlAgilityPack;

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
		}

		public async Task Run()
		{
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
}