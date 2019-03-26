using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DyflexisExport.Models;
using DyflexisExport.Services.Interfaces;
using HtmlAgilityPack;
using NLog;

namespace DyflexisExport.Services
{
	public class DyflexisEventsService
	{
		private static readonly Regex AssignmentIdRegex = new Regex("assignment://(?<id>\\d+)", RegexOptions.Compiled);

		private readonly IDyflexisHtmlScraper _dyflexisHtmlScraper;

		private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

		public DyflexisEventsService(IDyflexisHtmlScraper dyflexisHtmlScraper)
		{
			_dyflexisHtmlScraper = dyflexisHtmlScraper;
		}

		public async Task<IEnumerable<WorkAssignment>> GetAssignmentsForMonth(int year, int month)
		{
			if (year < 1900 || year > 3000)
				throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between 1900 and 3000, was: {year}");

			if (month < 1 || month > 12)
				throw new ArgumentOutOfRangeException(nameof(year), $"Month must be between 1 and 12, was: {month}");

			_logger.Info($"Scraping date {year}/{month}");
			
			var monthHtml = await _dyflexisHtmlScraper.GetMonthHtml(year, month);
			return monthHtml == null ? null : ParseMonthHtml(monthHtml).ToArray();
		}

		private IEnumerable<WorkAssignment> ParseMonthHtml(string html)
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

					_logger.Info($"Found appointment. Date: {date}, ID: {assignmentId}, Placement: {placement}, Start: {parsedStartTime:t}, End: {parsedEndTime:t}");

					yield return new WorkAssignment
					{
						Id = assignmentId,
						Start = parsedStartTime,
						End = parsedEndTime,
						Placement = placement
					};
				}
			}
		}
	}
}
