using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using DyflexisExport.Extensions;
using DyflexisExport.Models;
using DyflexisExport.Providers;
using Google.Apis.Calendar.v3.Data;
using NLog;

namespace DyflexisExport.Services
{
	public class ExportService : IStartable
	{
		private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
		private readonly AuthenticationService _authenticationService;
		private readonly CalendarServiceProvider _calendarServiceProvider;
		private readonly ConsoleControlService _consoleControlService;
		private readonly DyflexisEventsService _dyflexisEventsService;

		public ExportService(
			AuthenticationService authenticationService,
			CalendarServiceProvider calendarServiceProvider,
			ConsoleControlService consoleControlService,
			DyflexisEventsService dyflexisEventsService)
		{
			_authenticationService = authenticationService;
			_calendarServiceProvider = calendarServiceProvider;
			_consoleControlService = consoleControlService;
			_dyflexisEventsService = dyflexisEventsService;
		}

		private async Task<bool> SelectCalendar()
		{
			_logger.Trace("Choosing calendar.");

			CalendarList calendarList;
			using (var service = _calendarServiceProvider.GetCalendarService())
				calendarList = await service.CalendarList.List().ExecuteAsync();

			while (true)
			{
				for (var i = 0; i < calendarList.Items.Count; i++)
				{
					var calendar = calendarList.Items[i];
					Console.WriteLine($"[{i}] {calendar.Summary}");
				}

				Console.Write("Calendar nr.: ");

				if (!int.TryParse(Console.ReadLine(), out var chosenCalendarIndex) || chosenCalendarIndex < 0 || chosenCalendarIndex >= calendarList.Items.Count)
				{
					Console.WriteLine("Invalid number.");
					continue;
				}

				var chosenCalendar = calendarList.Items[chosenCalendarIndex];
				SettingsService.Settings.TargetCalendarId = chosenCalendar.Id;
				SettingsService.Save();

				_logger.Info($"Chosen calendar: [{chosenCalendar.Id}] {chosenCalendar.Summary}");

				return true;
			}
		}

		private async Task<bool> TryGetCalendar(string id)
		{
			try
			{
				using (var service = _calendarServiceProvider.GetCalendarService())
					return (await service.Calendars.Get(id).ExecuteAsync())?.Id != null;
			}
			catch (Exception e)
			{
				_logger.Error(e, $"Failed to get calendar with ID [{id}].");
			}

			return false;
		}

		private async Task<bool> EnsureCalendarExists()
		{
			if (string.IsNullOrEmpty(SettingsService.Settings.TargetCalendarId))
			{
				_logger.Warn("No target calendar has been selected, promting user.");
				await SelectCalendar();
			}

			while (true)
			{
				if (await TryGetCalendar(SettingsService.Settings.TargetCalendarId))
					return true;

				_logger.Warn("Re-selecting calendar, failed to get last specified calendar by ID.");

				Console.WriteLine("Failed to get the calendar, please select it again.");

				await SelectCalendar();
			}
		}

		private static bool EventRequiresUpdating(Event @event, WorkAssignment workAssignment)
		{
			return @event.Start.DateTime != workAssignment.Start ||
				@event.End.DateTime != workAssignment.End ||
				@event.Summary != workAssignment.Placement;
		}

		private async Task RefreshCalendarMonth(DateTime scapeDateTime, ICollection<WorkAssignment> workAssignments)
		{
			var service = _calendarServiceProvider.GetCalendarService();

			var listEventsRequest = service.Events.List(SettingsService.Settings.TargetCalendarId);
			listEventsRequest.TimeMin = scapeDateTime;
			listEventsRequest.TimeMax = scapeDateTime.AddMonths(1);
			var eventsResult = await listEventsRequest.ExecuteAsync();

			var assignmentEvents = eventsResult.Items.Where(e => e.Description?.StartsWith(WorkAssignmentExtensions.EventDescriptionPrefix) == true);
			var eventsToRemove = new List<Event>(assignmentEvents);
			
			foreach (var workAssignment in workAssignments)
			{
				var existingEvent = eventsToRemove.FirstOrDefault(e => e.Description == workAssignment.EventDescription());

				if (existingEvent == null)
				{
					_logger.Info($"Adding work assignment: {workAssignment.ToLogInfo()}");

					await service.Events.Insert(workAssignment.ToEvent(), SettingsService.Settings.TargetCalendarId).ExecuteAsync();
					continue;
				}

				_logger.Info($"Event already added: {existingEvent.ToLogInfo()}");

				eventsToRemove.Remove(existingEvent);

				await UpdateExistingEvent(service, workAssignment, existingEvent);
			}

			foreach (var eventToRemove in eventsToRemove)
			{
				_logger.Info($"Deleting event: {eventToRemove.ToLogInfo()}");

				await service.Events.Delete(SettingsService.Settings.TargetCalendarId, eventToRemove.Id).ExecuteAsync();
			}
		}

		private async Task UpdateExistingEvent(Google.Apis.Calendar.v3.CalendarService service, WorkAssignment workAssignment, Event existingEvent)
		{
			if (EventRequiresUpdating(existingEvent, workAssignment))
			{
				_logger.Info($"Event requires updating to: {workAssignment.ToLogInfo()}");

				await service.Events.Update(workAssignment.ToEvent(), SettingsService.Settings.TargetCalendarId, existingEvent.Id).ExecuteAsync();
			}
		}

		private async Task StartAsync()
		{
			if (!await _authenticationService.EnsureAuthenticated() || !await EnsureCalendarExists())
			{
				_consoleControlService.ShutDown();
				return;
			}

			var now = DateTime.Now;
			for (var month = 0; month < SettingsService.Settings.ScrapeMonthCount; month++)
			{
				var scrapeDate = new DateTime(now.Year, now.Month + month, 1, 0, 0, 0);

				var workAssignments = (await _dyflexisEventsService.GetAssignmentsForMonth(scrapeDate.Year, scrapeDate.Month)).ToList();
				await RefreshCalendarMonth(scrapeDate, workAssignments);
			}
		}

		public void Start()
		{
			Task.Run(StartAsync).Wait();
		}
	}
}
